namespace Easy.MessageHub
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    internal static class Subscriptions
    {
        private static readonly List<Subscription> AllSubscriptions = new List<Subscription>();
        private static int _subscriptionsChangeCounter;

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
                _subscriptionsChangeCounter++;
            }

            return key;
        }

        internal static void UnRegister(Guid token)
        {
            lock (AllSubscriptions)
            {
                var subscription = AllSubscriptions.Find(s => s.Token == token);
                if (subscription == null) { return; }

                var removed = AllSubscriptions.Remove(subscription);
                if (!removed) { return; }

                var localIdx = Array.IndexOf(_localSubscriptions, subscription);
                if (localIdx >= 0)
                {
                    _localSubscriptions = RemoveAt(_localSubscriptions, localIdx);
                }

                _subscriptionsChangeCounter++;
            }
        }

        internal static void Clear()
        {
            lock (AllSubscriptions)
            {
                AllSubscriptions.Clear();
                Array.Clear(_localSubscriptions, 0, _localSubscriptions.Length);
                _subscriptionsChangeCounter++;
            }
        }

        internal static bool IsRegistered(Guid token)
        {
            lock (AllSubscriptions) { return AllSubscriptions.Any(s => s.Token == token); }
        }

        internal static Subscription[] GetTheLatestSubscriptions()
        {
            if (_localSubscriptions == null) { _localSubscriptions = new Subscription[0]; }

            var changeCounterLatestCopy = Interlocked.CompareExchange(ref _subscriptionsChangeCounter, 0, 0);
            if (_localSubscriptionRevision == changeCounterLatestCopy) { return _localSubscriptions; }

            Subscription[] latestSubscriptions;
            lock (AllSubscriptions)
            {
                latestSubscriptions = AllSubscriptions.ToArray();
            }

            _localSubscriptionRevision = changeCounterLatestCopy;
            _localSubscriptions = latestSubscriptions;
            return _localSubscriptions;
        }

        internal static void Dispose() => Clear();

        private static T[] RemoveAt<T>(T[] source, int index)
        {
            var dest = new T[source.Length - 1];
            if (index > 0) { Array.Copy(source, 0, dest, 0, index); }

            if (index < source.Length - 1)
            {
                Array.Copy(source, index + 1, dest, index, source.Length - index - 1);
            }

            return dest;
        }
    }
}