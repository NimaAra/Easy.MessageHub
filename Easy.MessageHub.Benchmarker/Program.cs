namespace Easy.MessageHub.Benchmarker
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal class Program
    {
        private static readonly TimeSpan Duration = TimeSpan.FromSeconds(10);

        private static void Main()
        {
            HubSinglePublisherSingleSubscriber();
//            ClassicMethodSinglePublisherSingleSubscriber();

//            HubSinglePublisherMultipleSubscriber();
//            ClassicMethodSinglePublisherMultipleSubscriber();

//            HubMultiplePublisherSingleSubscriber();
//            ClassicMethodMultiplePublisherSingleSubscriber();

//            HubMultiplePublisherSingleSubscriberAndGlobalAuditHandler();

//            HubSinglePublisherMultipleSubscriberThrottled();

            Console.WriteLine($"Gen 0: {GC.CollectionCount(0).ToString()}");
            Console.WriteLine($"Gen 1: {GC.CollectionCount(1).ToString()}");
            Console.WriteLine($"Gen 2: {GC.CollectionCount(2).ToString()}");
        }

        public static void HubMultiplePublisherSingleSubscriberAndGlobalAuditHandler()
        {
            long globalCount = 0;
            long result = 0;
            var messageAgg = MessageHub.Instance;
            messageAgg.RegisterGlobalHandler((type, msg) => Interlocked.Increment(ref globalCount));

            Action<string> subscriber = msg => Interlocked.Increment(ref result);
            messageAgg.Subscribe(subscriber);

            var sw = Stopwatch.StartNew();
            Action action = () =>
            {
                while (sw.Elapsed < Duration)
                {
                    messageAgg.Publish("Hello there!");
                }
            };

            Parallel.Invoke(action, action, action, action, action);

            Console.WriteLine($"Result is: {result:n0} Time Taken: {sw.Elapsed}");
        }

        public static void HubMultiplePublisherSingleSubscriber()
        {
            long result = 0;
            var messageAgg = MessageHub.Instance;
            Action<string> subscriber = msg => result++;
            messageAgg.Subscribe(subscriber);

            var sw = Stopwatch.StartNew();
            var tasks = Enumerable.Range(0, 5).Select(n => Task.Factory.StartNew(() =>
            {
                while (true) { messageAgg.Publish("Hello there!"); }
            })).ToArray();

            Task.WaitAll(tasks, Duration);

            Console.WriteLine($"Result is: {result:n0} Time Taken: {sw.Elapsed}");
        }

        public static void HubSinglePublisherSingleSubscriber()
        {
            long result = 0;
            var messageAgg = MessageHub.Instance;
            Action<string> subscriber = msg => result++;
            messageAgg.Subscribe(subscriber);

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < Duration)
            {
                messageAgg.Publish("Hello there!");
            }

            Console.WriteLine($"Result is: {result:n0} Time Taken: {sw.Elapsed}");
        }

        public static void HubSinglePublisherMultipleSubscriber()
        {
            long result = 0;
            var messageAgg = MessageHub.Instance;
            Action<int> subscriber1 = msg => Interlocked.Increment(ref result);
            Action<int> subscriber2 = msg => Interlocked.Increment(ref result);
            Action<int> subscriber3 = msg => Interlocked.Increment(ref result);

            messageAgg.Subscribe(subscriber1);
            messageAgg.Subscribe(subscriber2);
            messageAgg.Subscribe(subscriber3);

            var counter = 0;
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < Duration)
            {
                messageAgg.Publish(counter++);
            }

            Console.WriteLine($"Result is: {result:n0} Time Taken: {sw.Elapsed}");
        }

        public static void HubSinglePublisherMultipleSubscriberThrottled()
        {
            long counter = 0;
            var messageAgg = MessageHub.Instance;
            messageAgg.RegisterGlobalHandler((type, msg) =>
            {
                Console.WriteLine($"Global: {DateTime.UtcNow:hh:mm:ss.fff} - {msg.ToString()}");
            });

            Action<long> subscriber = msg =>
            {
                Console.WriteLine($"Subscriber: {DateTime.UtcNow:hh:mm:ss.fff} - {msg.ToString()}");
            };

            messageAgg.Subscribe(subscriber, TimeSpan.FromSeconds(1));

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < Duration)
            {
                messageAgg.Publish(counter++);
                Thread.Sleep(100);
            }
        }

        public static void ClassicMethodMultiplePublisherSingleSubscriber()
        {
            long result = 0;

            var publisher = new Publisher<string>();

            var subscriber = new Subscriber<string>(publisher);
            subscriber.Subscribe(msg => Interlocked.Increment(ref result));

            var sw = Stopwatch.StartNew();
            Action action = () =>
            {
                while (sw.Elapsed < Duration)
                {
                    publisher.Publish("Hello there!");
                }
            };

            Parallel.Invoke(action, action, action, action, action);

            Console.WriteLine($"Result is: {result:n0} Time Taken: {sw.Elapsed}");
        }

        public static void ClassicMethodSinglePublisherSingleSubscriber()
        {
            long result = 0;

            var publisher = new Publisher<string>();

            var subscriber = new Subscriber<string>(publisher);
            subscriber.Subscribe(msg => result++);

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < Duration)
            {
                publisher.Publish("Hello there!");
            }

            Console.WriteLine($"Result is: {result:n0} Time Taken: {sw.Elapsed}");
        }

        public static void ClassicMethodSinglePublisherMultipleSubscriber()
        {
            long result = 0;

            var publisher = new Publisher<string>();

            Task.Factory.StartNew(() =>
            {
                var subscriber1 = new Subscriber<string>(publisher);
                subscriber1.Subscribe(msg => Interlocked.Increment(ref result));
            });

            Task.Factory.StartNew(() =>
            {
                var subscriber2 = new Subscriber<string>(publisher);
                subscriber2.Subscribe(msg => Interlocked.Increment(ref result));
            });

            Task.Factory.StartNew(() =>
            {
                var subscriber3 = new Subscriber<string>(publisher);
                subscriber3.Subscribe(msg => Interlocked.Increment(ref result));
            });

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < Duration)
            {
                publisher.Publish("Hello there!");
            }

            Console.WriteLine($"Result is: {result:n0} Time Taken: {sw.Elapsed}");
        }
    }

    public sealed class Publisher<T>
    {
        public void Publish(T message)
        {
            var copy = OnMessage;
            copy?.Invoke(this, new GenericEventArgs<T>(message));
        }

        public event EventHandler<GenericEventArgs<T>> OnMessage;
    }

    public sealed class GenericEventArgs<T> : EventArgs
    {
        public GenericEventArgs(T message)
        {
            Message = message;
        }

        public T Message { get; }
    }

    public sealed class Subscriber<T>
    {
        private readonly Publisher<T> _publisher;

        public Subscriber(Publisher<T> publisher)
        {
            _publisher = publisher;
        }

        public void Subscribe(Action<GenericEventArgs<T>> onMessage)
        {
            _publisher.OnMessage += (sender, msg) => onMessage(msg);
        }
    }

    public interface IEventPublisher
    {
        void Publish<TEvent>(TEvent sampleEvent);
        IObservable<TEvent> GetEvent<TEvent>();
    }
}
