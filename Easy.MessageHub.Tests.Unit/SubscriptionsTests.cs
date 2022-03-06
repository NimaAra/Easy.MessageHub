namespace Easy.MessageHub.Tests.Unit
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using Shouldly;

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
            subs.Clear(false);
            subs.IsRegistered(newKey).ShouldBeFalse();

            List<Subscription> subscriptionsSnapshotMain = subs.GetTheLatestSubscriptions();
            subscriptionsSnapshotMain.Count.ShouldBe(0);

            Guid keyA = subs.Register(TimeSpan.Zero, action);
            List<Subscription> subscriptionsSnapshotA = subs.GetTheLatestSubscriptions();
            subscriptionsSnapshotA.Count.ShouldBe(1);

            Guid keyB = subs.Register(TimeSpan.Zero, action);
            List<Subscription> subscriptionsSnapshotB = subs.GetTheLatestSubscriptions();
            subscriptionsSnapshotB.Count.ShouldBe(2);

            subs.IsRegistered(keyA).ShouldBeTrue();
            List<Subscription> subscriptionsSnapshotC = subs.GetTheLatestSubscriptions();
            subscriptionsSnapshotC.Count.ShouldBe(2);
            subscriptionsSnapshotC.ShouldBeSameAs(subscriptionsSnapshotB);

            subs.UnRegister(keyB);
            List<Subscription> subscriptionsSnapshotD = subs.GetTheLatestSubscriptions();
            subscriptionsSnapshotD.Count.ShouldBe(1);

            subs.Clear(false);
            List<Subscription> subscriptionsSnapshotE = subs.GetTheLatestSubscriptions();
            subscriptionsSnapshotE.Count.ShouldBe(0);
        }
    }
}