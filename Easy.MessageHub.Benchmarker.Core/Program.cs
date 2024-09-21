namespace Easy.MessageHub.Benchmarker.Core
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
            // HubSinglePublisherSingleSubscriber();
            // HubSinglePublisherSingleSubscriber();
            // ClassicMethodSinglePublisherSingleSubscriber();
            // ClassicMethodSinglePublisherSingleSubscriber();

            // HubSinglePublisherMultipleSubscriber();
            // HubSinglePublisherMultipleSubscriber();
            // ClassicMethodSinglePublisherMultipleSubscriber();
            // ClassicMethodSinglePublisherMultipleSubscriber();

            // HubMultiplePublisherSingleSubscriber();
            // HubMultiplePublisherSingleSubscriber();
            // ClassicMethodMultiplePublisherSingleSubscriber();
            // ClassicMethodMultiplePublisherSingleSubscriber();

            // HubMultiplePublisherSingleSubscriberAndGlobalAuditHandler();
            // HubMultiplePublisherSingleSubscriberAndGlobalAuditHandler();

            // HubSinglePublisherMultipleSubscriberThrottled();

            Console.Write($"Gen 0: {GC.CollectionCount(0).ToString()}");
            Console.Write($" | Gen 1: {GC.CollectionCount(1).ToString()}");
            Console.WriteLine($" | Gen 2: {GC.CollectionCount(2).ToString()}");
        }

        public static void HubMultiplePublisherSingleSubscriberAndGlobalAuditHandler()
        {
            long globalCount = 0;
            long result = 0;
            using MessageHub hub = new();
            hub.RegisterGlobalHandler((type, msg) => Interlocked.Increment(ref globalCount));

            Action<string> subscriber = msg => Interlocked.Increment(ref result);
            hub.Subscribe(subscriber);

            var sw = Stopwatch.StartNew();
            Action action = () =>
            {
                while (sw.Elapsed < Duration)
                {
                    hub.Publish("Hello there!");
                }
            };

            Parallel.Invoke(action, action, action, action, action);

            Console.WriteLine($"Result is: {result:n0} Time Taken: {sw.Elapsed}");
        }

        public static void HubMultiplePublisherSingleSubscriber()
        {
            long result = 0;
            using MessageHub hub = new();
            Action<string> subscriber = msg => result++;
            hub.Subscribe(subscriber);

            var sw = Stopwatch.StartNew();
            var tasks = Enumerable.Range(0, 5).Select(n => Task.Run(() =>
            {
                while (true) { hub.Publish("Hello there!"); }
            })).ToArray();

            Task.WaitAll(tasks, Duration);

            Console.WriteLine($"Result is: {result:n0} Time Taken: {sw.Elapsed}");
        }

        public static void HubSinglePublisherSingleSubscriber()
        {
            long result = 0;
            using MessageHub hub = new();
            Action<string> subscriber = msg => result++;
            hub.Subscribe(subscriber);

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < Duration)
            {
                hub.Publish("Hello there!");
            }

            Console.WriteLine($"Result is: {result:n0} Time Taken: {sw.Elapsed}");
        }

        public static void HubSinglePublisherMultipleSubscriber()
        {
            long result = 0;
            using MessageHub hub = new();
            Action<int> subscriber1 = msg => Interlocked.Increment(ref result);
            Action<int> subscriber2 = msg => Interlocked.Increment(ref result);
            Action<int> subscriber3 = msg => Interlocked.Increment(ref result);

            hub.Subscribe(subscriber1);
            hub.Subscribe(subscriber2);
            hub.Subscribe(subscriber3);

            var counter = 0;
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < Duration)
            {
                hub.Publish(counter++);
            }

            Console.WriteLine($"Result is: {result:n0} Time Taken: {sw.Elapsed}");
        }

        public static void HubSinglePublisherMultipleSubscriberThrottled()
        {
            long counter = 0;
            using MessageHub hub = new();
            hub.RegisterGlobalHandler((type, msg) =>
            {
                Console.WriteLine($"Global: {DateTime.UtcNow:hh:mm:ss.fff} - {msg.ToString()}");
            });

            Action<long> subscriber = msg =>
            {
                Console.WriteLine($"Subscriber: {DateTime.UtcNow:hh:mm:ss.fff} - {msg.ToString()}");
            };

            hub.Subscribe(subscriber, TimeSpan.FromSeconds(1));

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < Duration)
            {
                hub.Publish(counter++);
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

            Console.WriteLine($"Result is: {result:n0} Time Taken: {sw.Elapsed}");
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

        public Subscriber(Publisher<T> publisher) => _publisher = publisher;

        public void Subscribe(Action<T> onMessage) => _publisher.OnMessage += (sender, msg) => onMessage(msg);
    }
}