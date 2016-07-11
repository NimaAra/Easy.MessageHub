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
            var key = Subscriptions.Register(TimeSpan.Zero, action);
            key.ShouldNotBeNull();

            Subscriptions.IsRegistered(Guid.NewGuid()).ShouldBeFalse();
            Subscriptions.IsRegistered(key).ShouldBeTrue();
            Subscriptions.UnRegister(key);
            Subscriptions.IsRegistered(key).ShouldBeFalse();

            var newKey = Subscriptions.Register(TimeSpan.Zero, action);
            Subscriptions.IsRegistered(newKey).ShouldBeTrue();
            Subscriptions.Clear();
            Subscriptions.IsRegistered(newKey).ShouldBeFalse();

            var subscriptionsSnapshotMain = Subscriptions.GetTheLatestRevisionOfSubscriptions();
            subscriptionsSnapshotMain.Length.ShouldBe(0);

            var keyA = Subscriptions.Register(TimeSpan.Zero, action);
            var subscriptionsSnapshotA = Subscriptions.GetTheLatestRevisionOfSubscriptions();
            subscriptionsSnapshotA.Length.ShouldBe(1);

            var keyB = Subscriptions.Register(TimeSpan.Zero, action);
            var subscriptionsSnapshotB = Subscriptions.GetTheLatestRevisionOfSubscriptions();
            subscriptionsSnapshotB.Length.ShouldBe(2);

            Subscriptions.IsRegistered(keyA).ShouldBeTrue();
            var subscriptionsSnapshotC = Subscriptions.GetTheLatestRevisionOfSubscriptions();
            subscriptionsSnapshotC.Length.ShouldBe(2);
            subscriptionsSnapshotC.ShouldBeSameAs(subscriptionsSnapshotB);

            Subscriptions.UnRegister(keyB);
            var subscriptionsSnapshotD = Subscriptions.GetTheLatestRevisionOfSubscriptions();
            subscriptionsSnapshotD.Length.ShouldBe(1);

            Subscriptions.Clear();
            var subscriptionsSnapshotE = Subscriptions.GetTheLatestRevisionOfSubscriptions();
            subscriptionsSnapshotE.Length.ShouldBe(0);
        }
    }
}