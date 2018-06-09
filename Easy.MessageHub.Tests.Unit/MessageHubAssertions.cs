namespace Easy.MessageHub.Tests.Unit
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using Shouldly;
    
    public static class MessageHubAssertions
    {
        public static void Run()
        {
            When_publishing_with_no_subscribers();
            When_unsubscribing_invalid_token();
            When_subscribing_handlers();
            When_subscribing_same_handler_multiple_times();
            When_creating_multiple_instances_of_the_same_type_of_aggregator();
            When_subscribing_handlers_with_one_throwing_exception();
            When_testing_global_on_message_event();
            When_testing_single_subscriber_with_publisher_on_current_thread();
            When_testing_multiple_subscribers_with_publisher_on_current_thread();
            When_testing_multiple_subscribers_with_filters_and_publisher_on_current_thread();
            When_testing_multiple_subscribers_with_one_subscriber_unsubscribing_then_resubscribing();
            When_testing_handler_exists();
            When_operating_on_a_disposed_hub();
        }

        private static void When_publishing_with_no_subscribers()
        {
            var aggregator = MessageHub.Instance;
            Should.NotThrow(() => aggregator.Publish(TimeSpan.FromTicks(1234)));

            string result = null;
            aggregator.RegisterGlobalHandler((type, msg) =>
            {
                type.ShouldBe(typeof(string));
                msg.ShouldBeOfType<string>();
                result = msg as string;
            });

            aggregator.Publish("654321");
            result.ShouldBe("654321");
        }

        private static void When_unsubscribing_invalid_token()
        {
            var aggregator = MessageHub.Instance;
            Should.NotThrow(() => aggregator.Unsubscribe(Guid.NewGuid()));
        }

        private static void When_subscribing_handlers()
        {
            var aggregator = MessageHub.Instance;

            var queue = new ConcurrentQueue<string>();
            Action<string> subscriber = msg => queue.Enqueue(msg);

            aggregator.Subscribe(subscriber);

            aggregator.Publish("A");

            queue.Count.ShouldBe(1);

            string receivedMsg;
            queue.TryDequeue(out receivedMsg).ShouldBeTrue();
            receivedMsg.ShouldBe("A");
        }

        private static void When_subscribing_handlers_with_one_throwing_exception()
        {
            var hub = MessageHub.Instance;

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

        private static void When_subscribing_same_handler_multiple_times()
        {
            var aggregator = MessageHub.Instance;

            var totalMsgCount = 0;

            aggregator.RegisterGlobalHandler((type, msg) =>
            {
                type.ShouldBe(typeof(string));
                msg.ShouldBeOfType<string>();
                Interlocked.Increment(ref totalMsgCount);
            });

            var queue = new ConcurrentQueue<string>();
            Action<string> subscriber = msg => queue.Enqueue(msg);

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
            var aggregatorOne = MessageHub.Instance;
            var aggregatorTwo = MessageHub.Instance;

            aggregatorOne.ShouldBeSameAs(aggregatorTwo);
        }
        
        private static void When_testing_handler_exists()
        {
            var aggregator = MessageHub.Instance;
            aggregator.ClearSubscriptions();

            Action<string> subscriberOne = msg => { };
            var tokenOne = aggregator.Subscribe(subscriberOne);
            aggregator.IsSubscribed(tokenOne).ShouldBeTrue();

            Action<string> subscriberTwo = msg => { };
            var tokenTwo = aggregator.Subscribe(subscriberTwo);
            aggregator.IsSubscribed(tokenTwo).ShouldBeTrue();

            Action<string> subscriberThree = msg => { };
            var tokenThree = aggregator.Subscribe(subscriberThree);
            aggregator.IsSubscribed(tokenThree).ShouldBeTrue();

            Action<string> subscriberFour = msg => { };
            var tokenFour = aggregator.Subscribe(subscriberFour);
            aggregator.IsSubscribed(tokenFour).ShouldBeTrue();

            aggregator.Unsubscribe(tokenThree);
            aggregator.IsSubscribed(tokenThree).ShouldBeFalse();

            aggregator.Unsubscribe(tokenFour);
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
            var aggregator = MessageHub.Instance;
            aggregator.ClearSubscriptions();

            var msgOne = 0;

            aggregator.RegisterGlobalHandler((type, msg) =>
            {
                type.ShouldBe(typeof(string));
                msg.ShouldBeOfType<string>();
                msgOne++;
            });

            aggregator.Publish("A");

            msgOne.ShouldBe(1);

            aggregator.ClearSubscriptions();
            aggregator.Publish("B");

            msgOne.ShouldBe(2);

            var msgTwo = 0;

            aggregator.RegisterGlobalHandler((type, msg) =>
            {
                type.ShouldBe(typeof(string));
                msg.ShouldBeOfType<string>();
                msgTwo++;
            });

            aggregator.RegisterGlobalHandler((type, msg) =>
            {
                type.ShouldBe(typeof(string));
                msg.ShouldBeOfType<string>();
                msgTwo++;
            });

            aggregator.Publish("C");

            msgTwo.ShouldBe(1);

            aggregator.RegisterGlobalHandler((type, msg) =>
            {
                type.ShouldBe(typeof(string));
                msg.ShouldBeOfType<string>();
                // do nothing with the message
            });

            aggregator.Publish("D");

            msgOne.ShouldBe(2, "No handler would increment this value");
            msgTwo.ShouldBe(1, "No handler would increment this value");
        }

        private static void When_testing_single_subscriber_with_publisher_on_current_thread()
        {
            var aggregator = MessageHub.Instance;
            aggregator.ClearSubscriptions();

            var queue = new List<string>();

            Action<string> subscriber = msg => queue.Add(msg);
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
            var aggregator = MessageHub.Instance;
            aggregator.ClearSubscriptions();

            var queueOne = new List<string>();
            var queueTwo = new List<string>();

            Action<string> subscriberOne = msg => queueOne.Add("Sub1-" + msg);
            Action<string> subscriberTwo = msg => queueTwo.Add("Sub2-" + msg);

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
            var aggregator = MessageHub.Instance;
            aggregator.ClearSubscriptions();

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
            var aggregator = MessageHub.Instance;
            aggregator.ClearSubscriptions();

            aggregator.RegisterGlobalHandler((type, msg) =>
            {
                type.ShouldBe(typeof(string));
                msg.ShouldBeOfType<string>();
                Interlocked.Increment(ref totalMessages);
            });

            var queue = new List<string>();

            Action<string> subscriberOne = msg => queue.Add("Sub1-" + msg);
            Action<string> subscriberTwo = msg => queue.Add("Sub2-" + msg);

            var tokenOne = aggregator.Subscribe(subscriberOne);
            aggregator.Subscribe(subscriberTwo);

            aggregator.Publish("A");

            queue.Count.ShouldBe(2);
            queue[0].ShouldBe("Sub1-A");
            queue[1].ShouldBe("Sub2-A");

            aggregator.Unsubscribe(tokenOne);

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

        private static void When_operating_on_a_disposed_hub()
        {
            var totalMessages = 0;
            var aggregator = MessageHub.Instance;
            aggregator.RegisterGlobalHandler((type, msg) =>
            {
                type.ShouldBe(typeof(string));
                msg.ShouldBeOfType<string>();
                Interlocked.Increment(ref totalMessages);
            });

            var queue = new ConcurrentQueue<string>();

            Action<string> handler = msg => queue.Enqueue(msg);

            var token = aggregator.Subscribe(handler);

            aggregator.Dispose();

            Should.NotThrow(() => aggregator.Subscribe(handler));
            Should.NotThrow(() => aggregator.Unsubscribe(token));
            Should.NotThrow(() => aggregator.IsSubscribed(token));
            Should.NotThrow(() => aggregator.ClearSubscriptions());

            totalMessages.ShouldBe(0);
        }
    }
}
