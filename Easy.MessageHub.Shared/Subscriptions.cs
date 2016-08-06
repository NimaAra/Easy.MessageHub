namespace Easy.MessageHub
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal static class Subscriptions
    {
        private static readonly List<Subscription> AllSubscriptions = new List<Subscription>();
        private static int _subscriptionRevision;

        [ThreadStatic]
        private static int _localSubscriptionRevision;

        [ThreadStatic]
        private static Subscription[] _localSubscriptions;

        internal static Guid Register<T>(TimeSpan throttleBy, Action<T> action)
        {
            var type = typeof(T);
            var key = Guid.NewGuid();
            var subscription = new Subscription(type, key, throttleBy, action);

            lock (AllSubscriptions)
            {
                AllSubscriptions.Add(subscription);
                _subscriptionRevision++;
            }

            return key;
        }

        internal static void UnRegister(Guid token)
        {
            lock (AllSubscriptions)
            {
                var subscription = AllSubscriptions.FirstOrDefault(s => s.Token == token);
                var removed = AllSubscriptions.Remove(subscription);

                if (removed) { _subscriptionRevision++; }
            }
        }

        internal static void Clear()
        {
            lock (AllSubscriptions)
            {
                AllSubscriptions.Clear();
                _subscriptionRevision++;
            }
        }

        internal static bool IsRegistered(Guid token)
        {
            lock (AllSubscriptions) { return AllSubscriptions.Any(s => s.Token == token); }
        }

        internal static Subscription[] GetTheLatestRevisionOfSubscriptions()
        {
            if (_localSubscriptions == null)
            {
                _localSubscriptions = new Subscription[0];
            }

            if (_localSubscriptionRevision == _subscriptionRevision)
            {
                return _localSubscriptions;
            }

            Subscription[] latestSubscriptions;
            lock (AllSubscriptions)
            {
                latestSubscriptions = AllSubscriptions.ToArray();
                _localSubscriptionRevision = _subscriptionRevision;
            }

            _localSubscriptions = latestSubscriptions;

            return latestSubscriptions;
        }

        internal static void Dispose()
        {
            Clear();
        }
    }
}