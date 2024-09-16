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
        
        private ThreadLocal<int> _localSubscriptionRevision = 
            new ThreadLocal<int>(() => 0, true);

        private ThreadLocal<List<Subscription>> _localSubscriptions = 
            new ThreadLocal<List<Subscription>>(() => new List<Subscription>(), true);

        private bool _disposed;

        internal Guid Register<T>(TimeSpan throttleBy, Action<T> action)
        {
            var type = typeof(T);
            var key = Guid.NewGuid();
            var subscription = new Subscription(type, key, throttleBy, action);

            lock (AllSubscriptions)
            {
                CleanupThreadLocal();

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

                var localSubscriptionsValues = _localSubscriptions.Values;
                for (var i = 0; i < localSubscriptionsValues.Count; i++)
                {
                    var threadLocal = localSubscriptionsValues[i];
                    var localIdx = threadLocal.IndexOf(subscription);
                    if (localIdx < 0) { continue; }

                    threadLocal.RemoveAt(localIdx);
                }
                
                _subscriptionsChangeCounter++;

                CleanupThreadLocal();
            }
        }

        internal void Clear(bool dispose)
        {
            lock (AllSubscriptions)
            {
                if (_disposed) { return; }
                
                AllSubscriptions.Clear();

                var localSubscriptionsValues = _localSubscriptions.Values;
                for (var i = 0; i < localSubscriptionsValues.Count; i++)
                {
                    localSubscriptionsValues[i].Clear();
                }

                if (dispose)
                {
                    _localSubscriptionRevision.Dispose();
                    _localSubscriptions.Dispose();
                    _disposed = true;
                } 
                else 
                {
                    CleanupThreadLocal();
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
                CleanupThreadLocal();

                latestSubscriptions = AllSubscriptions.ToList();

                _localSubscriptionRevision.Value = changeCounterLatestCopy;
                _localSubscriptions.Value = latestSubscriptions;
                return _localSubscriptions.Value;
            }
        }

        /// <summary>
        /// Each new thread will increase the internal collection of ThreadLocal.Values
        /// 
        /// As ThreadLocal.Values doesn't handle the termination of the threads, we trigger the cleanup when ThreadLocal.Values is bigger than the total thread count
        /// </summary>
        private void CleanupThreadLocal()
        {
            if (_disposed)
                return;

            // As there are non-user threads, this won't be called too often
            if (_localSubscriptions.Values.Count <= System.Diagnostics.Process.GetCurrentProcess().Threads.Count)
                return;

            _localSubscriptionRevision.Dispose();
            _localSubscriptions.Dispose();

            _localSubscriptionRevision = new ThreadLocal<int>(() => 0, true);
            _localSubscriptions = new ThreadLocal<List<Subscription>>(() => new List<Subscription>(), true);
        }
    }
}