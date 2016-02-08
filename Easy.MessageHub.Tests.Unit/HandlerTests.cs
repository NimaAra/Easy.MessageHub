namespace Easy.MessageHub.Tests.Unit
{
    using System;
    using NUnit.Framework;
    using Shouldly;
    [TestFixture]
    public sealed class HandlerTests
    {
        [Test]
        public void When_creating_a_handler_with_null_action()
        {
            Should.Throw<ArgumentNullException>(() => new Handler<string>(null))
                .Message.ShouldBe("Action cannot be null\r\nParameter name: onMessage");
        } 

        [Test]
        public void When_creating_a_handler_with_null_predicate()
        {
            Should.Throw<ArgumentNullException>(() => new Handler<string>(msg => { }, null))
                .Message.ShouldBe("Predicate cannot be null\r\nParameter name: predicate");
        }

        [Test]
        public void When_creating_a_handler_with_no_predicate()
        {
            string result = null;
            var handler = new Handler<string>(msg => result = "Message is: " + msg);

            handler.Handle("A");
            result.ShouldBe("Message is: A");

            handler.Handle("AB");
            result.ShouldBe("Message is: AB");

            handler.Handle("ABC");
            result.ShouldBe("Message is: ABC");

            handler.Handle("ABCD");
            result.ShouldBe("Message is: ABCD");
        }

        [Test]
        public void When_creating_a_handler_with_predicate()
        {
            string result = null;
            var handler = new Handler<string>(
                msg => result = "Message is: " + msg,
                msg => msg.Length < 3);

            handler.Handle("A");
            result.ShouldBe("Message is: A");

            handler.Handle("AB");
            result.ShouldBe("Message is: AB");

            handler.Handle("ABC");
            result.ShouldBe("Message is: AB");

            handler.Handle("ABCD");
            result.ShouldBe("Message is: AB");
        }
    }
}