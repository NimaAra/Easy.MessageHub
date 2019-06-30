namespace Easy.MessageHub.Tests.Unit
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using NUnit.Framework;
    using Shouldly;
    
    [TestFixture]
    internal sealed class MessageHubTests
    {
        [Test]
        public void When_publishing_with_no_subscribers()
        {
            var hub = new MessageHub();
            Should.NotThrow(() => hub.Publish(TimeSpan.FromTicks(1234)));

            string result = null;
            hub.RegisterGlobalHandler((type, msg) =>
            {
                type.ShouldBe(typeof(string));
                msg.ShouldBeOfType<string>();
                result = msg as string;
            });

            hub.Publish("654321");
            result.ShouldBe("654321");
        }

        [Test]
        public void When_unsubscribing_invalid_token()
        {
            var hub = new MessageHub();
            Should.NotThrow(() => hub.Unsubscribe(Guid.NewGuid()));
        }

        [Test]
        public void When_subscribing_handlers()
        {
            var hub = new MessageHub();

            var queue = new ConcurrentQueue<string>();
            Action<string> subscriber = msg => queue.Enqueue(msg);

            hub.Subscribe(subscriber);

            hub.Publish("A");

            queue.Count.ShouldBe(1);

            string receivedMsg;
            queue.TryDequeue(out receivedMsg).ShouldBeTrue();
            receivedMsg.ShouldBe("A");
        }

        [Test]
        public void When_subscribing_handlers_with_one_throwing_exception()
        {
            var hub = new MessageHub();

            var queue = new List<string>();
            var totalMsgs = new List<string>();
            var errors = new List<KeyValuePair<Guid, Exception>>();

            hub.RegisterGlobalHandler((type, msg) =>
            {
                type.ShouldBe(typeof(string));
                msg.ShouldBeOfType<string>();
                totalMsgs.Add((string)msg);
            });

            hub.RegisterGlobalErrorHandler(
                (token, e) => errors.Add(new KeyValuePair<Guid, Exception>(token, e)));
            
            Action<string> subscriberOne = msg => queue.Add("Sub1-" + msg);
            Action<string> subscriberTwo = msg => { throw new InvalidOperationException("Ooops-" + msg); };
            Action<string> subscriberThree = msg => queue.Add("Sub3-" + msg);

            hub.Subscribe(subscriberOne);
            var subTwoToken = hub.Subscribe(subscriberTwo);
            hub.Subscribe(subscriberThree);
            hub.Publish("A");

            Action<string> subscriberFour = msg => { throw new InvalidCastException("Aaargh-" + msg); };
            var subFourToken = hub.Subscribe(subscriberFour);

            hub.Publish("B");

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
                err.Value.GetType() == typeof(InvalidOperationException)
                && err.Value.Message == "Ooops-A"
                && err.Key == subTwoToken);

            errors.ShouldContain(err =>
                err.Value.GetType() == typeof(InvalidOperationException)
                && err.Value.Message == "Ooops-B"
                && err.Key == subTwoToken);

            errors.ShouldContain(err =>
                err.Value.GetType() == typeof(InvalidCastException)
                && err.Value.Message == "Aaargh-B"
                && err.Key == subFourToken);
        }

        [Test]
        public void When_subscribing_same_handler_multiple_times()
        {
            var hub = new MessageHub();

            var totalMsgCount = 0;

            hub.RegisterGlobalHandler((type, msg) =>
            {
                type.ShouldBe(typeof(string));
                msg.ShouldBeOfType<string>();
                Interlocked.Increment(ref totalMsgCount);
            });

            var queue = new ConcurrentQueue<string>();
            Action<string> subscriber = msg => queue.Enqueue(msg);

            var tokenOne = hub.Subscribe(subscriber);
            var tokenTwo = hub.Subscribe(subscriber);

            hub.IsSubscribed(tokenOne);
            hub.IsSubscribed(tokenTwo);

            hub.Publish("A");

            queue.Count.ShouldBe(2);
            totalMsgCount.ShouldBe(1);
        }

        [Test]
        public void When_creating_multiple_instances_of_the_same_type_of_hub()
        {
            var hub1 = new MessageHub();
            var hub2 = new MessageHub();

            hub1.ShouldNotBeSameAs(hub2);
        }

        [Test]
        public void When_testing_handler_exists()
        {
            var hub = new MessageHub();
            hub.ClearSubscriptions();

            Action<string> subscriberOne = msg => { };
            var tokenOne = hub.Subscribe(subscriberOne);
            hub.IsSubscribed(tokenOne).ShouldBeTrue();

            Action<string> subscriberTwo = msg => { };
            var tokenTwo = hub.Subscribe(subscriberTwo);
            hub.IsSubscribed(tokenTwo).ShouldBeTrue();

            Action<string> subscriberThree = msg => { };
            var tokenThree = hub.Subscribe(subscriberThree);
            hub.IsSubscribed(tokenThree).ShouldBeTrue();

            Action<string> subscriberFour = msg => { };
            var tokenFour = hub.Subscribe(subscriberFour);
            hub.IsSubscribed(tokenFour).ShouldBeTrue();

            hub.Unsubscribe(tokenThree);
            hub.IsSubscribed(tokenThree).ShouldBeFalse();

            hub.Unsubscribe(tokenFour);
            hub.IsSubscribed(tokenFour).ShouldBeFalse();

            hub.IsSubscribed(tokenTwo).ShouldBeTrue();
            hub.IsSubscribed(tokenOne).ShouldBeTrue();

            hub.ClearSubscriptions();

            hub.IsSubscribed(tokenOne).ShouldBeFalse();
            hub.IsSubscribed(tokenTwo).ShouldBeFalse();
            hub.IsSubscribed(tokenThree).ShouldBeFalse();
            hub.IsSubscribed(tokenFour).ShouldBeFalse();

            // now let's add back one subscription
            tokenFour = hub.Subscribe(subscriberFour);
            hub.IsSubscribed(tokenFour).ShouldBeTrue();
        }

        [Test]
        public void When_testing_global_on_message_event()
        {
            var hub = new MessageHub();
            hub.ClearSubscriptions();

            var msgOne = 0;

            hub.RegisterGlobalHandler((type, msg) =>
            {
                type.ShouldBe(typeof(string));
                msg.ShouldBeOfType<string>();
                msgOne++;
            });

            hub.Publish("A");

            msgOne.ShouldBe(1);

            hub.ClearSubscriptions();
            hub.Publish("B");

            msgOne.ShouldBe(2);

            var msgTwo = 0;

            hub.RegisterGlobalHandler((type, msg) =>
            {
                type.ShouldBe(typeof(string));
                msg.ShouldBeOfType<string>();
                msgTwo++;
            });

            hub.RegisterGlobalHandler((type, msg) =>
            {
                type.ShouldBe(typeof(string));
                msg.ShouldBeOfType<string>();
                msgTwo++;
            });

            hub.Publish("C");

            msgTwo.ShouldBe(1);

            hub.RegisterGlobalHandler((type, msg) =>
            {
                type.ShouldBe(typeof(string));
                msg.ShouldBeOfType<string>();
                // do nothing with the message
            });

            hub.Publish("D");

            msgOne.ShouldBe(2, "No handler would increment this value");
            msgTwo.ShouldBe(1, "No handler would increment this value");
        }

        [Test]
        public void When_testing_single_subscriber_with_publisher_on_current_thread()
        {
            var hub = new MessageHub();
            hub.ClearSubscriptions();

            var queue = new List<string>();

            Action<string> subscriber = msg => queue.Add(msg);
            hub.Subscribe(subscriber);

            hub.Publish("MessageA");

            queue.Count.ShouldBe(1);
            queue[0].ShouldBe("MessageA");

            hub.Publish("MessageB");

            queue.Count.ShouldBe(2);
            queue[1].ShouldBe("MessageB");
        }

        [Test]
        public void When_testing_multiple_subscribers_with_publisher_on_current_thread()
        {
            var hub = new MessageHub();
            hub.ClearSubscriptions();

            var queueOne = new List<string>();
            var queueTwo = new List<string>();

            Action<string> subscriberOne = msg => queueOne.Add("Sub1-" + msg);
            Action<string> subscriberTwo = msg => queueTwo.Add("Sub2-" + msg);

            hub.Subscribe(subscriberOne);
            hub.Subscribe(subscriberTwo);

            hub.Publish("MessageA");

            queueOne.Count.ShouldBe(1);
            queueTwo.Count.ShouldBe(1);

            queueOne[0].ShouldBe("Sub1-MessageA");
            queueTwo[0].ShouldBe("Sub2-MessageA");

            hub.Publish("MessageB");

            queueOne.Count.ShouldBe(2);
            queueTwo.Count.ShouldBe(2);

            queueOne[1].ShouldBe("Sub1-MessageB");
            queueTwo[1].ShouldBe("Sub2-MessageB");
        }

        [Test]
        public void When_testing_multiple_subscribers_with_filters_and_publisher_on_current_thread()
        {
            var hub = new MessageHub();
            hub.ClearSubscriptions();

            var queueOne = new List<string>();
            var queueTwo = new List<string>();

            var predicateOne = new Predicate<string>(x => x.Length > 3);
            var predicateTwo = new Predicate<string>(x => x.Length < 3);

            Action<string> subscriberOne = msg =>
            {
                if (predicateOne(msg))
                {
                    queueOne.Add("Sub1-" + msg);
                }
            };

            Action<string> subscriberTwo = msg =>
            {
                if (predicateTwo(msg))
                {
                    queueTwo.Add("Sub2-" + msg);
                }
            };

            hub.Subscribe(subscriberOne);
            hub.Subscribe(subscriberTwo);

            hub.Publish("MessageA");

            queueOne.Count.ShouldBe(1);
            queueTwo.Count.ShouldBe(0);
            queueOne[0].ShouldBe("Sub1-MessageA");

            hub.Publish("MA");

            queueTwo.Count.ShouldBe(1);
            queueOne.Count.ShouldBe(1);
            queueTwo[0].ShouldBe("Sub2-MA");

            hub.Publish("MMM");

            queueOne.Count.ShouldBe(1);
            queueTwo.Count.ShouldBe(1);

            hub.Publish("MessageB");

            queueOne.Count.ShouldBe(2);
            queueTwo.Count.ShouldBe(1);
            queueOne[1].ShouldBe("Sub1-MessageB");

            hub.Publish("MB");

            queueTwo.Count.ShouldBe(2);
            queueOne.Count.ShouldBe(2);
            queueTwo[1].ShouldBe("Sub2-MB");
        }

        [Test]
        public void When_testing_multiple_subscribers_with_one_subscriber_unsubscribing_then_resubscribing()
        {
            var totalMessages = 0;
            var hub = new MessageHub();
            hub.ClearSubscriptions();

            hub.RegisterGlobalHandler((type, msg) =>
            {
                type.ShouldBe(typeof(string));
                msg.ShouldBeOfType<string>();
                Interlocked.Increment(ref totalMessages);
            });

            var queue = new List<string>();

            Action<string> subscriberOne = msg => queue.Add("Sub1-" + msg);
            Action<string> subscriberTwo = msg => queue.Add("Sub2-" + msg);

            var tokenOne = hub.Subscribe(subscriberOne);
            hub.Subscribe(subscriberTwo);

            hub.Publish("A");

            queue.Count.ShouldBe(2);
            queue[0].ShouldBe("Sub1-A");
            queue[1].ShouldBe("Sub2-A");

            hub.Unsubscribe(tokenOne);

            hub.Publish("B");

            queue.Count.ShouldBe(3);
            queue[2].ShouldBe("Sub2-B");

            hub.Subscribe(subscriberOne);

            hub.Publish("C");

            queue.Count.ShouldBe(5);
            queue[3].ShouldBe("Sub2-C");
            queue[4].ShouldBe("Sub1-C");

            Thread.Sleep(TimeSpan.FromSeconds(1));
            totalMessages.ShouldBe(3);
        }

        [Test]
        public void When_operating_on_a_disposed_hub()
        {
            var totalMessages = 0;
            var hub = new MessageHub();
            hub.RegisterGlobalHandler((type, msg) =>
            {
                type.ShouldBe(typeof(string));
                msg.ShouldBeOfType<string>();
                Interlocked.Increment(ref totalMessages);
            });

            var queue = new ConcurrentQueue<string>();

            Action<string> handler = msg => queue.Enqueue(msg);

            var token = hub.Subscribe(handler);

            hub.Dispose();

            Should.NotThrow(() => hub.Subscribe(handler));
            Should.NotThrow(() => hub.Unsubscribe(token));
            Should.NotThrow(() => hub.IsSubscribed(token));
            Should.NotThrow(() => hub.ClearSubscriptions());

            totalMessages.ShouldBe(0);
        }

        [Test]
        public void When_using_multiple_hubs()
        {
            var hub1 = new MessageHub();
            var hub2 = new MessageHub();

            var totalMessages = new List<string>();

            hub1.RegisterGlobalHandler((type, msg) => totalMessages.Add((string)msg));
            hub2.RegisterGlobalHandler((type, msg) => totalMessages.Add((string)msg));

            var hub1Messages = new List<string>();
            var hub2Messages = new List<string>();

            hub1.Subscribe<string>(x => hub1Messages.Add(x));
            hub2.Subscribe<string>(x => hub2Messages.Add(x));
            
            hub1.Publish("A");
            
            hub2.Publish("B");
            hub2.Publish("C");

            totalMessages.ShouldBe(new [] {"A", "B", "C"});

            hub1Messages.ShouldBe(new [] {"A"});
            hub2Messages.ShouldBe(new [] {"B", "C"});

            hub1.Dispose();
            hub2.Dispose();
        }

        [Test]
        public void When_disposing_one_of_the_hubs()
        {
            var hub1 = new MessageHub();
            var hub2 = new MessageHub();

            var totalMessages = new List<string>();

            hub1.RegisterGlobalHandler((type, msg) => totalMessages.Add((string)msg));
            hub2.RegisterGlobalHandler((type, msg) => totalMessages.Add((string)msg));

            var hub1Messages = new List<string>();
            var hub2Messages = new List<string>();

            hub1.Subscribe<string>(x => hub1Messages.Add(x));
            hub2.Subscribe<string>(x => hub2Messages.Add(x));

            hub1.Publish("A");

            hub2.Publish("B");
            hub2.Publish("C");

            hub1.Dispose();

            Should.Throw<ObjectDisposedException>(() => hub1.Publish("C"))
                .Message.ShouldBe("Cannot access a disposed object.\r\nObject name: 'The ThreadLocal object has been disposed.'.");
            
            hub2.Publish("D");

            totalMessages.ShouldBe(new[] { "A", "B", "C", "D" });

            hub1Messages.ShouldBe(new[] { "A" });
            hub2Messages.ShouldBe(new[] { "B", "C", "D" });

            hub2.Dispose();
        }
    }
}
