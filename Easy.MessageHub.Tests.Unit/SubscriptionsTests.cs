namespace Easy.MessageHub.Tests.Unit
{
    using NUnit.Framework;
    using Shouldly;
    using System;

    [TestFixture]
    internal sealed class SubscriptionsTests
    {
        [Test]
        public void When_subscribing_value_type()
        {
            Action<int> action = n => { };
            
            Subscriptions subs = new Subscriptions();

            Guid key = subs.Register(TimeSpan.Zero, action);
            key.ShouldNotBe(default);

            subs.IsRegistered(Guid.NewGuid()).ShouldBeFalse();
            subs.IsRegistered(key).ShouldBeTrue();
            subs.UnRegister(key);
            subs.IsRegistered(key).ShouldBeFalse();

            Guid newKey = subs.Register(TimeSpan.Zero, action);
            subs.IsRegistered(newKey).ShouldBeTrue();
            subs.Clear();
            subs.IsRegistered(newKey).ShouldBeFalse();

            Subscription[] buffer = new Subscription[3];

            int count = subs.GetTheLatestSubscriptions(buffer);
            count.ShouldBe(0);
            
            Subscription[] subscriptionsSnapshotMain = buffer;
            subscriptionsSnapshotMain.ShouldBe([default, default, default]);

            Guid keyA = subs.Register(TimeSpan.Zero, action);
            count = subs.GetTheLatestSubscriptions(buffer);
            count.ShouldBe(1);

            Span<Subscription> subscriptionsSnapshotA = buffer.AsSpan(0, count);
            Subscription[] remainderBuffer = buffer.AsSpan(count).ToArray();
            remainderBuffer.ShouldBe([default, default]);
            subscriptionsSnapshotA[0].Token.ShouldBe(keyA);

            Guid keyB = subs.Register(TimeSpan.Zero, action);
            count = subs.GetTheLatestSubscriptions(buffer);
            count.ShouldBe(2);

            Span<Subscription> subscriptionsSnapshotB = buffer.AsSpan(0, count);
            remainderBuffer = buffer.AsSpan(count).ToArray();
            remainderBuffer.ShouldBe([default]);
            subscriptionsSnapshotB[0].Token.ShouldBe(keyA);
            subscriptionsSnapshotB[1].Token.ShouldBe(keyB);

            subs.IsRegistered(keyA).ShouldBeTrue();
            
            subs.UnRegister(keyB);
            count = subs.GetTheLatestSubscriptions(buffer);
            count.ShouldBe(1);

            Span<Subscription> subscriptionsSnapshotC = buffer.AsSpan(0, count);
            subscriptionsSnapshotC[0].Token.ShouldBe(keyA);
            
            subs.Clear();
            count = subs.GetTheLatestSubscriptions(buffer);
            count.ShouldBe(0);
        }
    }
}