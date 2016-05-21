namespace Easy.MessageHub.Tests.Unit
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using Shouldly;

    [TestFixture]
    public sealed class MessageHubTests
    {
        [Test]
        public void Run()
        {
            UsageExamples();
            MessageHubAssertions.Run();
        }

        private static void UsageExamples()
        {
            var hub = MessageHub.Instance;

            // As the hub is a singleton, we have to add this as a hack to allow 
            // multiple assertions each with their own GlobalHandler registered to pass
            var isUsageExampleRunning = true;

            var errors = new Queue<MessageHubErrorEventArgs>();
            hub.OnError += (sender, args) => errors.Enqueue(args);

            var auditQueue = new Queue<MessageBase>();

            var allMessagesQueue = new Queue<MessageBase>();
            var commandsQueue = new Queue<Command>();
            var ordersQueue = new Queue<Order>();

            Action<Type, object> auditHandler = (type, msg) =>
            {
                // ReSharper disable once AccessToModifiedClosure
                if (!isUsageExampleRunning) { return; }

                msg.ShouldBeAssignableTo<MessageBase>();
                auditQueue.Enqueue((MessageBase) msg);
            };

            hub.RegisterGlobalHandler(auditHandler);

            hub.Publish(new MessageBase { Name = "Base" });

            commandsQueue.ShouldBeEmpty();
            ordersQueue.ShouldBeEmpty();

            auditQueue.Count.ShouldBe(1);
            auditQueue.Dequeue().Name.ShouldBe("Base");

            allMessagesQueue.ShouldBeEmpty();

            hub.Subscribe<MessageBase>(msg =>
            {
                msg.ShouldBeAssignableTo<MessageBase>();
                allMessagesQueue.Enqueue(msg);
            });

            hub.Subscribe<Command>(msg =>
            {
                msg.ShouldBeAssignableTo<MessageBase>();
                msg.ShouldBeAssignableTo<Command>();
                commandsQueue.Enqueue(msg);
            });

            hub.Subscribe<OpenCommand>(msg =>
            {
                msg.ShouldBeAssignableTo<Command>();
                msg.ShouldBeOfType<OpenCommand>();
                commandsQueue.Enqueue(msg);
            });

            hub.Subscribe<CloseCommand>(msg =>
            {
                msg.ShouldBeAssignableTo<Command>();
                msg.ShouldBeOfType<CloseCommand>(); 
                commandsQueue.Enqueue(msg);
            });

            hub.Subscribe<Order>(msg =>
            {
                msg.ShouldBeOfType<Order>();
                ordersQueue.Enqueue(msg);
            });

            hub.Publish(new Command { Name = "Command" });

            auditQueue.Count.ShouldBe(1);
            auditQueue.Dequeue().Name.ShouldBe("Command");

            allMessagesQueue.Count.ShouldBe(1);
            allMessagesQueue.Dequeue().Name.ShouldBe("Command");

            commandsQueue.Count.ShouldBe(1);
            commandsQueue.Dequeue().Name.ShouldBe("Command");

            ordersQueue.ShouldBeEmpty();

            hub.Publish(new Order { Name = "Order1" });

            auditQueue.Count.ShouldBe(1);
            auditQueue.Dequeue().Name.ShouldBe("Order1");

            allMessagesQueue.Count.ShouldBe(1);
            allMessagesQueue.Dequeue().Name.ShouldBe("Order1");

            commandsQueue.ShouldBeEmpty();

            ordersQueue.Count.ShouldBe(1);
            ordersQueue.Dequeue().Name.ShouldBe("Order1");

            hub.Subscribe(new Action<Order>(o => ordersQueue.Enqueue(o)));

            hub.Publish(new Order { Name = "Order2" });

            auditQueue.Count.ShouldBe(1);
            auditQueue.Dequeue().Name.ShouldBe("Order2");

            allMessagesQueue.Count.ShouldBe(1);
            allMessagesQueue.Dequeue().Name.ShouldBe("Order2");

            ordersQueue.Count.ShouldBe(2);
            ordersQueue.Dequeue().Name.ShouldBe("Order2");
            ordersQueue.Dequeue().Name.ShouldBe("Order2");

            commandsQueue.ShouldBeEmpty();

            hub.Publish(new OpenCommand { Name = "OpenCommand"});

            auditQueue.Count.ShouldBe(1);
            auditQueue.Dequeue().Name.ShouldBe("OpenCommand");

            allMessagesQueue.Count.ShouldBe(1);
            allMessagesQueue.Dequeue().Name.ShouldBe("OpenCommand");

            ordersQueue.ShouldBeEmpty();

            commandsQueue.Count.ShouldBe(2);

            errors.ShouldBeEmpty("No errors should have occurred!");

            isUsageExampleRunning = false;
        }
    }

    internal class MessageBase
    {
        public string Name { get; set; }
    }

    internal class Command : MessageBase
    {

    }

    internal sealed class OpenCommand : Command
    {

    }

    internal sealed class CloseCommand : Command
    {

    }

    internal sealed class Order : MessageBase
    {

    }
}