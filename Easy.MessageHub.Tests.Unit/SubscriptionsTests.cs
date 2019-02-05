namespace Easy.MessageHub.Tests.Unit
{
    using System;
    using NUnit.Framework;
    using Shouldly;

    [TestFixture]
    internal sealed class SubscriptionsTests
    {
        [Test]
        public void When_subscribing_value_type()
        {
            Action<int> action = n => { };
            
            var subs = new Subscriptions();

            var key = subs.Register(TimeSpan.Zero, action);
            key.ShouldNotBeNull();

            subs.IsRegistered(Guid.NewGuid()).ShouldBeFalse();
            subs.IsRegistered(key).ShouldBeTrue();
            subs.UnRegister(key);
            subs.IsRegistered(key).ShouldBeFalse();

            var newKey = subs.Register(TimeSpan.Zero, action);
            subs.IsRegistered(newKey).ShouldBeTrue();
            subs.Clear(false);
            subs.IsRegistered(newKey).ShouldBeFalse();

            var subscriptionsSnapshotMain = subs.GetTheLatestSubscriptions();
            subscriptionsSnapshotMain.Count.ShouldBe(0);

            var keyA = subs.Register(TimeSpan.Zero, action);
            var subscriptionsSnapshotA = subs.GetTheLatestSubscriptions();
            subscriptionsSnapshotA.Count.ShouldBe(1);

            var keyB = subs.Register(TimeSpan.Zero, action);
            var subscriptionsSnapshotB = subs.GetTheLatestSubscriptions();
            subscriptionsSnapshotB.Count.ShouldBe(2);

            subs.IsRegistered(keyA).ShouldBeTrue();
            var subscriptionsSnapshotC = subs.GetTheLatestSubscriptions();
            subscriptionsSnapshotC.Count.ShouldBe(2);
            subscriptionsSnapshotC.ShouldBeSameAs(subscriptionsSnapshotB);

            subs.UnRegister(keyB);
            var subscriptionsSnapshotD = subs.GetTheLatestSubscriptions();
            subscriptionsSnapshotD.Count.ShouldBe(1);

            subs.Clear(false);
            var subscriptionsSnapshotE = subs.GetTheLatestSubscriptions();
            subscriptionsSnapshotE.Count.ShouldBe(0);
        }
    }
}