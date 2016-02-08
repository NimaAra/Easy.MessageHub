namespace Easy.MessageHub.Tests.Unit
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using Shouldly;

    [TestFixture]
    public sealed class UsageExample
    {
        [Test]
        public void Run()
        {
            var hub = MessageHub<MessageBase>.Instance;

            var auditQueue = new Queue<MessageBase>();
            var resultQueue = new Queue<MessageBase>();

            hub.RegisterGlobalHandler(msg => auditQueue.Enqueue(msg));
            hub.Publish(new MessageBase { Name = "Base" });

            auditQueue.Count.ShouldBe(1);
            auditQueue.Dequeue().Name.ShouldBe("Base");

            hub.Subscribe<MessageBase>(msg =>
            {
                msg.ShouldBeOfType<MessageBase>();
                resultQueue.Enqueue(msg);
            });

            hub.Subscribe<OpenCommand>(msg =>
            {
                msg.ShouldBeOfType<OpenCommand>();
                resultQueue.Enqueue(msg);
            });

            hub.Subscribe<CloseCommand>(msg =>
            {
                msg.ShouldBeOfType<CloseCommand>();
                resultQueue.Enqueue(msg);
            });

            hub.Subscribe<Order>(msg =>
            {
                msg.ShouldBeOfType<Order>();
                resultQueue.Enqueue(msg);
            });

            hub.Publish(new Command { Name = "Command" });

            auditQueue.Count.ShouldBe(1);
            auditQueue.Dequeue().Name.ShouldBe("Command");

            resultQueue.ShouldBeEmpty();

            hub.Publish(new Order { Name = "Order1" });

            auditQueue.Count.ShouldBe(1);
            auditQueue.Dequeue().Name.ShouldBe("Order1");

            resultQueue.Count.ShouldBe(1);
            resultQueue.Dequeue().Name.ShouldBe("Order1");

            hub.Subscribe(new Handler<Order>(o => resultQueue.Enqueue(o)));

            hub.Publish(new Order { Name = "Order2" });

            auditQueue.Count.ShouldBe(1);
            auditQueue.Dequeue().Name.ShouldBe("Order2");

            resultQueue.Count.ShouldBe(2);
            resultQueue.Dequeue().Name.ShouldBe("Order2");
            resultQueue.Dequeue().Name.ShouldBe("Order2");
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