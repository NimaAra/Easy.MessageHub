namespace Easy.MessageHub
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;

    [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
    internal class Subscriptions
    {
        private readonly List<Subscription> AllSubscriptions = new List<Subscription>();
        private int _subscriptionsChangeCounter;

        private readonly ThreadLocal<int> _localSubscriptionRevision = 
            new ThreadLocal<int>(() => 0, true);

        private readonly ThreadLocal<List<Subscription>> _localSubscriptions = 
            new ThreadLocal<List<Subscription>>(() => new List<Subscription>(), true);

        private bool _disposed;

        internal Guid Register<T>(TimeSpan throttleBy, Action<T> action)
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

        internal void UnRegister(Guid token)
        {
            lock (AllSubscriptions)
            {
                var idx = AllSubscriptions.FindIndex(s => s.Token == token);
                
                if (idx < 0) { return; }

                var subscription = AllSubscriptions[idx];

                AllSubscriptions.RemoveAt(idx);

                for (var i = 0; i < _localSubscriptions.Values.Count; i++)
                {
                    var threadLocal = _localSubscriptions.Values[i];
                    var localIdx = threadLocal.IndexOf(subscription);
                    if (localIdx < 0) { continue; }

                    threadLocal.RemoveAt(localIdx);
                }
                
                _subscriptionsChangeCounter++;
            }
        }

        internal void Clear(bool dispose)
        {
            lock (AllSubscriptions)
            {
                if (_disposed) { return; }
                
                AllSubscriptions.Clear();

                for (var i = 0; i < _localSubscriptions.Values.Count; i++)
                {
                    _localSubscriptions.Values[i].Clear();
                }

                if (dispose)
                {
                    _localSubscriptionRevision.Dispose();
                    _localSubscriptions.Dispose();
                    _disposed = true;
                } 
                else 
                {
                    _subscriptionsChangeCounter++;
                }
            }
        }

        internal bool IsRegistered(Guid token)
        {
            lock (AllSubscriptions) { return AllSubscriptions.Any(s => s.Token == token); }
        }

        internal List<Subscription> GetTheLatestSubscriptions()
        {
            var changeCounterLatestCopy = Interlocked.CompareExchange(
                ref _subscriptionsChangeCounter, 0, 0);
            
            if (_localSubscriptionRevision.Value == changeCounterLatestCopy)
            {
                return _localSubscriptions.Value;
            }

            List<Subscription> latestSubscriptions;
            lock (AllSubscriptions)
            {
                latestSubscriptions = AllSubscriptions.ToList();
            }

            _localSubscriptionRevision.Value = changeCounterLatestCopy;
            _localSubscriptions.Value = latestSubscriptions;
            return _localSubscriptions.Value;
        }
    }
}