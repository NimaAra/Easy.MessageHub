namespace EasyMessageHub.Tests.Unit
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using NUnit.Framework;
    using Shouldly;

    [TestFixture]
    public sealed class MessageHubTests
    {
        [Test]
        public void Run()
        {
            When_publishing_with_no_subscribers();
            When_un_subscribing_invalid_token();
            When_subscribing_handlers();
            When_subscribing_same_handler_multiple_times();
            When_creating_multiple_instances_of_the_same_type_of_aggregator();
            When_creating_multpile_instances_of_different_type_of_aggregator();
            When_subscribing_handlers_with_one_throwing_exception();
            When_testing_global_on_message_event();
            When_testing_single_subscriber_with_publisher_on_current_thread();
            When_testing_multiple_subscribers_with_publisher_on_current_thread();
            When_testing_multiple_subscribers_with_filters_and_publisher_on_current_thread();
            When_testing_multiple_subscribers_with_one_subscriber_unsubscribing_then_resubscribing();
            When_testing_handler_exists();
            When_operating_on_a_disposed_aggregator();
        }

        private static void When_publishing_with_no_subscribers()
        {
            var aggregator = MessageHub<TimeSpan>.Instance;
            Should.NotThrow(() => aggregator.Publish(TimeSpan.FromTicks(1234)));

            TimeSpan result = TimeSpan.Zero;
            aggregator.RegisterGlobalHandler(msg => result = msg);
            aggregator.Publish(TimeSpan.FromTicks(654321));
            result.ShouldBe(TimeSpan.FromTicks(654321));
        }

        private static void When_un_subscribing_invalid_token()
        {
            var aggregator = MessageHub<string>.Instance;

            Should.NotThrow(() => aggregator.UnSubscribe(Guid.NewGuid()));
        }

        private static void When_subscribing_handlers()
        {
            var aggregator = MessageHub<string>.Instance;

            var queue = new ConcurrentQueue<string>();
            var subscriber = new Handler<string>(msg => queue.Enqueue(msg));

            aggregator.Subscribe(subscriber);

            aggregator.Publish("A");

            queue.Count.ShouldBe(1);

            string receivedMsg;
            queue.TryDequeue(out receivedMsg).ShouldBeTrue();
            receivedMsg.ShouldBe("A");
        }

        public static void When_subscribing_handlers_with_one_throwing_exception()
        {
            var aggregator = MessageHub<string>.Instance;

            var queue = new List<string>();
            var totalMsgs = new List<string>();
            var errors = new List<MessageHubErrorEventArgs>();

            aggregator.RegisterGlobalHandler(msg => totalMsgs.Add(msg));
            aggregator.OnError += (sender, e) => errors.Add(e);

            var subscriberOne = new Handler<string>(msg => queue.Add("Sub1-" + msg));
            var subscriberTwo = new Handler<string>(msg => { throw new InvalidOperationException("Ooops-" + msg); });
            var subscriberThree = new Handler<string>(msg => queue.Add("Sub3-" + msg));

            aggregator.Subscribe(subscriberOne);
            var subTwoToken = aggregator.Subscribe(subscriberTwo);
            aggregator.Subscribe(subscriberThree);
            aggregator.Publish("A");

            var subscriberFour = new Handler<string>(msg => { throw new InvalidCastException("Aaargh-" + msg); });
            var subFourToken = aggregator.Subscribe(subscriberFour);

            aggregator.Publish("B");

            queue.Count.ShouldBe(4);
            queue[0].ShouldBe("Sub1-A");
            queue[1].ShouldBe("Sub3-A");
            queue[2].ShouldBe("Sub1-B");
            queue[3].ShouldBe("Sub3-B");

            totalMsgs.Count.ShouldBe(2);
            totalMsgs.ShouldContain(msg => msg == "A");
            totalMsgs.ShouldContain(msg => msg == "B");

            errors.Count.ShouldBe(3);
            errors.ShouldContain(err =>
                err.Exception.GetType() == typeof(InvalidOperationException)
                && err.Exception.Message == "Ooops-A"
                && err.Token == subTwoToken);

            errors.ShouldContain(err =>
                err.Exception.GetType() == typeof(InvalidOperationException)
                && err.Exception.Message == "Ooops-B"
                && err.Token == subTwoToken);

            errors.ShouldContain(err =>
                err.Exception.GetType() == typeof(InvalidCastException)
                && err.Exception.Message == "Aaargh-B"
                && err.Token == subFourToken);
        }

        public static void When_subscribing_same_handler_multiple_times()
        {
            var aggregator = MessageHub<string>.Instance;

            var totalMsgCount = 0;
            aggregator.RegisterGlobalHandler(msg => Interlocked.Increment(ref totalMsgCount));

            var queue = new ConcurrentQueue<string>();
            var subscriber = new Handler<string>(msg => queue.Enqueue(msg));

            var tokenOne = aggregator.Subscribe(subscriber);
            var tokenTwo = aggregator.Subscribe(subscriber);

            aggregator.IsSubscribed(tokenOne);
            aggregator.IsSubscribed(tokenTwo);

            aggregator.Publish("A");

            queue.Count.ShouldBe(2);
            totalMsgCount.ShouldBe(1);
        }

        private static void When_creating_multiple_instances_of_the_same_type_of_aggregator()
        {
            var aggregatorOne = MessageHub<string>.Instance;
            var aggregatorTwo = MessageHub<string>.Instance;

            aggregatorOne.ShouldBeSameAs(aggregatorTwo);
        }

        private static void When_creating_multpile_instances_of_different_type_of_aggregator()
        {
            var aggregatorOne = MessageHub<Stopwatch>.Instance;
            var aggregatorTwo = MessageHub<Exception>.Instance;

            aggregatorOne.ShouldNotBeSameAs(aggregatorTwo);
        }

        private static void When_testing_handler_exists()
        {
            var aggregator = MessageHub<string>.Instance;
            aggregator.ClearSubscriptions();

            var subscriberOne = new Handler<string>(msg => { });
            var tokenOne = aggregator.Subscribe(subscriberOne);
            aggregator.IsSubscribed(tokenOne).ShouldBeTrue();

            var subscriberTwo = new Handler<string>(msg => { });
            var tokenTwo = aggregator.Subscribe(subscriberTwo);
            aggregator.IsSubscribed(tokenTwo).ShouldBeTrue();

            var subscriberThree = new Handler<string>(msg => { });
            var tokenThree = aggregator.Subscribe(subscriberThree);
            aggregator.IsSubscribed(tokenThree).ShouldBeTrue();

            var subscriberFour = new Handler<string>(msg => { });
            var tokenFour = aggregator.Subscribe(subscriberFour);
            aggregator.IsSubscribed(tokenFour).ShouldBeTrue();

            aggregator.UnSubscribe(tokenThree);
            aggregator.IsSubscribed(tokenThree).ShouldBeFalse();

            aggregator.UnSubscribe(tokenFour);
            aggregator.IsSubscribed(tokenFour).ShouldBeFalse();

            aggregator.IsSubscribed(tokenTwo).ShouldBeTrue();
            aggregator.IsSubscribed(tokenOne).ShouldBeTrue();

            aggregator.ClearSubscriptions();

            aggregator.IsSubscribed(tokenOne).ShouldBeFalse();
            aggregator.IsSubscribed(tokenTwo).ShouldBeFalse();
            aggregator.IsSubscribed(tokenThree).ShouldBeFalse();
            aggregator.IsSubscribed(tokenFour).ShouldBeFalse();

            // now let's add back one subscription
            tokenFour = aggregator.Subscribe(subscriberFour);
            aggregator.IsSubscribed(tokenFour).ShouldBeTrue();
        }

        private static void When_testing_global_on_message_event()
        {
            var aggregator = MessageHub<string>.Instance;
            aggregator.ClearSubscriptions();

            var msgOne = 0;
            aggregator.RegisterGlobalHandler(msg => msgOne++);
            aggregator.Publish("A");

            msgOne.ShouldBe(1);

            aggregator.ClearSubscriptions();
            aggregator.Publish("B");

            msgOne.ShouldBe(2);

            var msgTwo = 0;
            aggregator.RegisterGlobalHandler(msg => msgTwo++);
            aggregator.RegisterGlobalHandler(msg => msgTwo++);
            
            aggregator.Publish("C");

            msgTwo.ShouldBe(1);

            aggregator.RegisterGlobalHandler(msgg => { });
            aggregator.Publish("D");

            msgOne.ShouldBe(2, "No handler would increment this value");
            msgTwo.ShouldBe(1, "No handler would increment this value");
        }

        private static void When_testing_single_subscriber_with_publisher_on_current_thread()
        {
            var aggregator = MessageHub<string>.Instance;
            aggregator.ClearSubscriptions();

            var queue = new List<string>();

            var subscriber = new Handler<string>(msg => queue.Add(msg));
            aggregator.Subscribe(subscriber);

            aggregator.Publish("MessageA");

            queue.Count.ShouldBe(1);
            queue[0].ShouldBe("MessageA");

            aggregator.Publish("MessageB");

            queue.Count.ShouldBe(2);
            queue[1].ShouldBe("MessageB");
        }

        private static void When_testing_multiple_subscribers_with_publisher_on_current_thread()
        {
            var aggregator = MessageHub<string>.Instance;
            aggregator.ClearSubscriptions();

            var queueOne = new List<string>();
            var queueTwo = new List<string>();

            var subscriberOne = new Handler<string>(msg => queueOne.Add("Sub1-" + msg));
            var subscriberTwo = new Handler<string>(msg => queueTwo.Add("Sub2-" + msg));

            aggregator.Subscribe(subscriberOne);
            aggregator.Subscribe(subscriberTwo);

            aggregator.Publish("MessageA");

            queueOne.Count.ShouldBe(1);
            queueTwo.Count.ShouldBe(1);

            queueOne[0].ShouldBe("Sub1-MessageA");
            queueTwo[0].ShouldBe("Sub2-MessageA");

            aggregator.Publish("MessageB");

            queueOne.Count.ShouldBe(2);
            queueTwo.Count.ShouldBe(2);

            queueOne[1].ShouldBe("Sub1-MessageB");
            queueTwo[1].ShouldBe("Sub2-MessageB");
        }

        private static void When_testing_multiple_subscribers_with_filters_and_publisher_on_current_thread()
        {
            var aggregator = MessageHub<string>.Instance;
            aggregator.ClearSubscriptions();

            var queueOne = new List<string>();
            var queueTwo = new List<string>();

            var subscriberOne = new Handler<string>(msg => queueOne.Add("Sub1-" + msg), msg => msg.Length > 3);
            var subscriberTwo = new Handler<string>(msg => queueTwo.Add("Sub2-" + msg), msg => msg.Length < 3);

            aggregator.Subscribe(subscriberOne);
            aggregator.Subscribe(subscriberTwo);

            aggregator.Publish("MessageA");

            queueOne.Count.ShouldBe(1);
            queueTwo.Count.ShouldBe(0);
            queueOne[0].ShouldBe("Sub1-MessageA");

            aggregator.Publish("MA");

            queueTwo.Count.ShouldBe(1);
            queueOne.Count.ShouldBe(1);
            queueTwo[0].ShouldBe("Sub2-MA");

            aggregator.Publish("MMM");

            queueOne.Count.ShouldBe(1);
            queueTwo.Count.ShouldBe(1);

            aggregator.Publish("MessageB");

            queueOne.Count.ShouldBe(2);
            queueTwo.Count.ShouldBe(1);
            queueOne[1].ShouldBe("Sub1-MessageB");

            aggregator.Publish("MB");

            queueTwo.Count.ShouldBe(2);
            queueOne.Count.ShouldBe(2);
            queueTwo[1].ShouldBe("Sub2-MB");
        }

        private static void When_testing_multiple_subscribers_with_one_subscriber_unsubscribing_then_resubscribing()
        {
            var totalMessages = 0;
            var aggregator = MessageHub<string>.Instance;
            aggregator.ClearSubscriptions();
            aggregator.RegisterGlobalHandler(msg => Interlocked.Increment(ref totalMessages));

            var queue = new List<string>();

            var subscriberOne = new Handler<string>(msg => queue.Add("Sub1-" + msg));
            var subscriberTwo = new Handler<string>(msg => queue.Add("Sub2-" + msg));

            var tokenOne = aggregator.Subscribe(subscriberOne);
            aggregator.Subscribe(subscriberTwo);

            aggregator.Publish("A");

            queue.Count.ShouldBe(2);
            queue[0].ShouldBe("Sub1-A");
            queue[1].ShouldBe("Sub2-A");

            aggregator.UnSubscribe(tokenOne);

            aggregator.Publish("B");

            queue.Count.ShouldBe(3);
            queue[2].ShouldBe("Sub2-B");

            aggregator.Subscribe(subscriberOne);

            aggregator.Publish("C");

            queue.Count.ShouldBe(5);
            queue[3].ShouldBe("Sub2-C");
            queue[4].ShouldBe("Sub1-C");

            Thread.Sleep(TimeSpan.FromSeconds(1));
            totalMessages.ShouldBe(3);
        }

        private static void When_operating_on_a_disposed_aggregator()
        {
            var totalMessages = 0;
            var aggregator = MessageHub<string>.Instance;
            aggregator.RegisterGlobalHandler(msg => Interlocked.Increment(ref totalMessages));

            var queue = new ConcurrentQueue<string>();

            var handler = new Handler<string>(msg => queue.Enqueue(msg));

            var token = aggregator.Subscribe(handler);

            aggregator.Dispose();

            Should.Throw<ObjectDisposedException>(() => aggregator.Subscribe(handler))
                .Message.ShouldBe($"Cannot access a disposed object.\r\nObject name: '{aggregator.GetType().Name}'.");

            Should.Throw<ObjectDisposedException>(() => aggregator.Publish("hi"))
                .Message.ShouldBe($"Cannot access a disposed object.\r\nObject name: '{aggregator.GetType().Name}'.");

            Should.Throw<ObjectDisposedException>(() => aggregator.Publish("hi"))
                .Message.ShouldBe($"Cannot access a disposed object.\r\nObject name: '{aggregator.GetType().Name}'.");

            Should.Throw<ObjectDisposedException>(() => aggregator.UnSubscribe(token))
                .Message.ShouldBe($"Cannot access a disposed object.\r\nObject name: '{aggregator.GetType().Name}'.");

            Should.Throw<ObjectDisposedException>(() => aggregator.IsSubscribed(token))
                .Message.ShouldBe($"Cannot access a disposed object.\r\nObject name: '{aggregator.GetType().Name}'.");

            Should.Throw<ObjectDisposedException>(() => aggregator.ClearSubscriptions())
                .Message.ShouldBe($"Cannot access a disposed object.\r\nObject name: '{aggregator.GetType().Name}'.");

            totalMessages.ShouldBe(0);
        }
    }
}
