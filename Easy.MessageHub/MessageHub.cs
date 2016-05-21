namespace Easy.MessageHub
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;

    /// <summary>
    /// An implementation of the <c>Event Aggregator</c> pattern.
    /// </summary>
    public sealed class MessageHub : IMessageHub
    {
        private static readonly Lazy<MessageHub> Lazy = new Lazy<MessageHub>(() => new MessageHub(), true);

        private readonly List<KeyValuePair<Type, Subscription>> _allSubscriptions;

        private readonly ThreadLocal<int> _localSubscriptionRevision;
        private readonly ThreadLocal<KeyValuePair<Type, Subscription>[]> _localSubscriptions;

        private Action<Type, object> _globalHandler = (type, msg) => { };
        private int _subscriptionRevision;
        private int _disposed;

        private MessageHub()
        {
            _allSubscriptions = new List<KeyValuePair<Type, Subscription>>();

            _localSubscriptionRevision = new ThreadLocal<int>(() => 0);
            _localSubscriptions = new ThreadLocal<KeyValuePair<Type, Subscription>[]>(
                () => new KeyValuePair<Type, Subscription>[0]);
        }

        /// <summary>
        /// Returns a single instance of the <see cref="MessageHub"/>
        /// </summary>
        public static MessageHub Instance => Lazy.Value;

        /// <summary>
        /// Invoked if an error occurs when publishing the message to a subscriber.
        /// </summary>
        public event EventHandler<MessageHubErrorEventArgs> OnError;

        /// <summary>
        /// Registers a callback which is invoked on every message published by the <see cref="MessageHub"/>.
        /// <remarks>Invoking this method with a new <paramref name="onMessage"/>overwrites the previous one.</remarks>
        /// </summary>
        /// <param name="onMessage">
        /// The callback to invoke on every message
        /// <remarks>The callback receives the type of the message and the message as arguments</remarks>
        /// </param>
        public void RegisterGlobalHandler(Action<Type, object> onMessage)
        {
            EnsureNotNull(onMessage);
            EnsureNotDisposed();

            _globalHandler = onMessage;
        }

        /// <summary>
        /// Publishes the <paramref name="message"/> on the <see cref="MessageHub"/>.
        /// </summary>
        /// <param name="message">The message to published</param>
        public void Publish<T>(T message)
        {
            PublishImpl(message);
        }

        /// <summary>
        /// Subscribes a callback against the <see cref="MessageHub"/> for a specific type of message.
        /// </summary>
        /// <typeparam name="T">The type of message to subscribe to</typeparam>
        /// <param name="action">The callback to be invoked once the message is published on the <see cref="MessageHub"/></param>
        /// <returns>The token representing the subscription</returns>
        public Guid Subscribe<T>(Action<T> action)
        {
            EnsureNotNull(action);
            EnsureNotDisposed();

            var token = Guid.NewGuid();
            var subscription = new Subscription(token, o => action((T)o));

            lock (_allSubscriptions)
            {
                _allSubscriptions.Add(new KeyValuePair<Type, Subscription>(typeof(T), subscription));
                _subscriptionRevision++;
            }

            return token;
        }
        
        /// <summary>
        /// Un-Subscribes a subscription from the <see cref="MessageHub"/>.
        /// </summary>
        /// <param name="token">The token representing the subscription</param>
        public void UnSubscribe(Guid token)
        {
            EnsureNotNull(token);
            EnsureNotDisposed();

            lock (_allSubscriptions)
            {
                var subscription = _allSubscriptions.FirstOrDefault(s => s.Value.Token == token);
                var removed = _allSubscriptions.Remove(subscription);

                if (removed)
                {
                    _subscriptionRevision++;
                }
            }
        }

        /// <summary>
        /// Checks if a specific subscription is active on the <see cref="MessageHub"/>.
        /// </summary>
        /// <param name="token">The token representing the subscription</param>
        /// <returns><c>True</c> if the subscription is active otherwise <c>False</c></returns>
        public bool IsSubscribed(Guid token)
        {
            EnsureNotNull(token);
            EnsureNotDisposed();

            lock (_allSubscriptions)
            {
                return _allSubscriptions.Any(s => s.Value.Token == token);
            }
        }

        /// <summary>
        /// Clears all the subscriptions from the <see cref="MessageHub"/>.
        /// <remarks>The global handler and the <see cref="OnError"/> are not affected</remarks>
        /// </summary>
        public void ClearSubscriptions()
        {
            EnsureNotDisposed();

            lock (_allSubscriptions)
            {
                _allSubscriptions.Clear();
                _subscriptionRevision++;
            }
        }

        /// <summary>
        /// Disposes the <see cref="MessageHub"/>.
        /// </summary>
        public void Dispose()
        {
            Interlocked.Increment(ref _disposed);
            _allSubscriptions.Clear();
            _localSubscriptions.Dispose();
            _localSubscriptionRevision.Dispose();
        }

        /// <summary> 
        /// Updates the publisher thread's copy of the subscriptions to the latest version.
        /// </summary> 
        private KeyValuePair<Type, Subscription>[] GetTheLatestRevisionOfSubscriptions()
        {
            if (_localSubscriptionRevision.Value == _subscriptionRevision)
            {
                return _localSubscriptions.Value;
            }

            KeyValuePair<Type, Subscription>[] latestSubscriptions;
            lock (_allSubscriptions)
            {
                latestSubscriptions = _allSubscriptions.ToArray();

                _localSubscriptions.Value = latestSubscriptions;
                _localSubscriptionRevision.Value = _subscriptionRevision;
            }

            return latestSubscriptions;
        }

        /// <summary>
        /// The method which publishes the passed in <paramref name="message"/>.
        /// </summary>
        /// <param name="message">The message to publish</param>
        private void PublishImpl<T>(T message)
        {
            EnsureNotDisposed();

            var localSubscriptions = GetTheLatestRevisionOfSubscriptions();

            var msgType = typeof(T);

            _globalHandler(msgType, message);

            // ReSharper disable once ForCanBeConvertedToForeach | Performance Critical
            for (var idx = 0; idx < localSubscriptions.Length; idx++)
            {
                EnsureNotDisposed();

                var subKeyVal = localSubscriptions[idx];

                var subscriptionType = subKeyVal.Key;
                var subscription = subKeyVal.Value;

                if (!subscriptionType.IsAssignableFrom(msgType)) { continue; }

                var handler = subscription.Handle;

                try
                {
                    handler(message);
                }
                catch (Exception e)
                {
                    var copy = OnError;
                    copy?.Invoke(this, new MessageHubErrorEventArgs(e, subscription.Token));
                }
            }
        }

        [DebuggerStepThrough]
        private void EnsureNotDisposed()
        {
            if (_disposed == 1) { throw new ObjectDisposedException(GetType().Name); }
        }

        [DebuggerStepThrough]
        private void EnsureNotNull(object obj)
        {
            if (obj == null) { throw new NullReferenceException(nameof(obj)); }
        }

        private sealed class Subscription
        {
            public Subscription(Guid token, Action<object> handler)
            {
                Token = token;
                Handle = handler;
            }

            public Guid Token { get; }
            public Action<object> Handle { get; }
        }
    }
}
