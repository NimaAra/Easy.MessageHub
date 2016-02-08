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
//            MessageAggregatorSinglePublisherSingleSubscriber();
//            ClassicMethodSinglePublisherSingleSubscriber();
//
//            MessageAggregatorSinglePublisherMultipleSubscriber();
//            ClassicMethodSinglePublisherMultipleSubscriber();
//
            MessageAggregatorMultiplePublisherSingleSubscriber();
//            ClassicMethodMultiplePublisherSingleSubscriber();

//            MessageAggregatorMultiplePublisherSingleSubscriberAndGlobalAuditHandler();

            Console.WriteLine("Gen 0: {0}", GC.CollectionCount(0));
            Console.WriteLine("Gen 1: {0}", GC.CollectionCount(1));
            Console.WriteLine("Gen 2: {0}", GC.CollectionCount(2));

            Console.ReadLine();
        }

        public static void MessageAggregatorMultiplePublisherSingleSubscriberAndGlobalAuditHandler()
        {
            long globalCount = 0;
            long result = 0;
            var messageAgg = MessageHub<string>.Instance;
            messageAgg.RegisterGlobalHandler(msg => Interlocked.Increment(ref globalCount));

            var subscriber = new Handler<string>(msg => Interlocked.Increment(ref result));
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

            Console.WriteLine("Result is: {0:n0} Time Taken: {1}", result, sw.Elapsed);
        }

        public static void MessageAggregatorMultiplePublisherSingleSubscriber()
        {
            long result = 0;
            var messageAgg = MessageHub<string>.Instance;
            var subscriber = new Handler<string>(msg => result++);
            messageAgg.Subscribe(subscriber);

            var sw = Stopwatch.StartNew();
            var tasks = Enumerable.Range(0, 5).Select(n => Task.Run(() =>
            {
                while (true) { messageAgg.Publish("Hello there!"); }
            })).ToArray();

            Task.WaitAll(tasks, Duration);

            Console.WriteLine("Result is: {0:n0} Time Taken: {1}", result, sw.Elapsed);
        }

        public static void MessageAggregatorSinglePublisherSingleSubscriber()
        {
            long result = 0;
            var messageAgg = MessageHub<string>.Instance;
            var subscriber = new Handler<string>(msg => result++);
            messageAgg.Subscribe(subscriber);

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < Duration)
            {
                messageAgg.Publish("Hello there!");
            }

            Console.WriteLine("Result is: {0:n0} Time Taken: {1}", result, sw.Elapsed);
        }

        public static void MessageAggregatorSinglePublisherMultipleSubscriber()
        {
            long result = 0;
            var messageAgg = MessageHub<string>.Instance;
            var subscriber1 = new Handler<string>(msg => Interlocked.Increment(ref result));
            var subscriber2 = new Handler<string>(msg => Interlocked.Increment(ref result));
            var subscriber3 = new Handler<string>(msg => Interlocked.Increment(ref result));

            messageAgg.Subscribe(subscriber1);
            messageAgg.Subscribe(subscriber2);
            messageAgg.Subscribe(subscriber3);

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < Duration)
            {
                messageAgg.Publish("Hello there!");
            }

            Console.WriteLine("Result is: {0:n0} Time Taken: {1}", result, sw.Elapsed);
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

            Console.WriteLine("Result is: {0:n0} Time Taken: {1}", result, sw.Elapsed);
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

            Console.WriteLine("Result is: {0:n0} Time Taken: {1}", result, sw.Elapsed);
        }

        public static void ClassicMethodSinglePublisherMultipleSubscriber()
        {
            long result = 0;

            var publisher = new Publisher<string>();

            Task.Run(() =>
            {
                var subscriber1 = new Subscriber<string>(publisher);
                subscriber1.Subscribe(msg => Interlocked.Increment(ref result));
            });

            Task.Run(() =>
            {
                var subscriber2 = new Subscriber<string>(publisher);
                subscriber2.Subscribe(msg => Interlocked.Increment(ref result));
            });

            Task.Run(() =>
            {
                var subscriber3 = new Subscriber<string>(publisher);
                subscriber3.Subscribe(msg => Interlocked.Increment(ref result));
            });

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < Duration)
            {
                publisher.Publish("Hello there!");
            }

            Console.WriteLine("Result is: {0:n0} Time Taken: {1}", result, sw.Elapsed);
        }
    }

    public sealed class Publisher<T>
    {
        public void Publish(T message)
        {
            var copy = OnMessage;
            copy?.Invoke(this, message);
        }

        public event EventHandler<T> OnMessage;
    }

    public sealed class Subscriber<T>
    {
        private readonly Publisher<T> _publisher;

        public Subscriber(Publisher<T> publisher)
        {
            _publisher = publisher;
        }

        public void Subscribe(Action<T> onMessage)
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
