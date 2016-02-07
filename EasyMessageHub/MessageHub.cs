namespace EasyMessageHub
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    /// <summary>
    /// An implementation of the <c>Event Aggregator</c> pattern.
    /// </summary>
    /// <typeparam name="TMsgBase">The type of the message to be published</typeparam>
    public sealed class MessageHub<TMsgBase> : IMessageHub<TMsgBase>
    {
        private static readonly Lazy<MessageHub<TMsgBase>> Lazy = new Lazy<MessageHub<TMsgBase>>(() => new MessageHub<TMsgBase>(), true);

        private readonly List<Subscription> _subscriptions;
        private readonly ThreadLocal<int> _localSubscriptionRevision;
        private readonly ThreadLocal<Subscription[]> _localSubscriptions;

        private Action<TMsgBase> _globalHandler = msg => { };
        private int _subscriptionRevision;
        private int _disposed;

        private MessageHub()
        {
            _subscriptions = new List<Subscription>();
            _localSubscriptionRevision = new ThreadLocal<int>(() => 0);
            _localSubscriptions = new ThreadLocal<Subscription[]>(() => _subscriptions.ToArray());
        }

        /// <summary>
        /// Returns a single instance of the <see cref="MessageHub{TMsgBase}"/>
        /// </summary>
        public static MessageHub<TMsgBase> Instance => Lazy.Value;

        /// <summary>
        /// Invoked if an error occurs when publishing the message to a subscriber.
        /// </summary>
        public event EventHandler<MessageHubErrorEventArgs> OnError;

        /// <summary>
        /// Registers a callback which is invoked on every message published by the <see cref="MessageHub{TMsgBase}"/>
        /// <remarks>Invoking this method with a new <paramref name="onMessage"/>overwrites the previous one.</remarks>
        /// </summary>
        /// <param name="onMessage">The callback to invoke on every message</param>
        public void RegisterGlobalHandler(Action<TMsgBase> onMessage)
        {
            EnsureNotNull(onMessage);
            EnsureNotDisposed();

            _globalHandler = onMessage;
        }

        /// <summary>
        /// Publishes the <paramref name="message"/>.
        /// </summary>
        /// <param name="message">The message to be published.</param>
        public void Publish(TMsgBase message)
        {
            PublishImpl(message);
        }

        /// <summary>
        /// Subscribes a handler against the <see cref="MessageHub{TMsgBase}"/> for a certain type of message.
        /// </summary>
        /// <typeparam name="TMsg">The type of message to subscribe to.</typeparam>
        /// <param name="handler">The handler to be invoked once the message is published by the <see cref="MessageHub{TMsgBase}"/></param>
        /// <returns>The token representing the subscription</returns>
        public Guid Subscribe<TMsg>(Handler<TMsg> handler) where TMsg : TMsgBase
        {
            EnsureNotNull(handler);
            EnsureNotDisposed();

            var token = Guid.NewGuid();

            lock (_subscriptions)
            {
                _subscriptions.Add(new Subscription(
                    token,
                    new Handler<TMsgBase>(msg => handler.Handle((TMsg)msg))));
                _subscriptionRevision++;
            }

            return token;
        }

        /// <summary>
        /// Subscribes a callback and a predicate against the <see cref="MessageHub{TMsgBase}"/> for a certain type of message.
        /// </summary>
        /// <typeparam name="TMsg">The type of message to subscribe to.</typeparam>
        /// <param name="action">The callback to be invoked once the message is published by the <see cref="MessageHub{TMsgBase}"/></param>
        /// <param name="predicate">The predicate which indicates whether the message should be published to the subscriber or not</param>
        /// <returns>The token representing the subscription</returns>
        public Guid Subscribe<TMsg>(Action<TMsg> action, Predicate<TMsg> predicate = null) where TMsg : TMsgBase
        {
            EnsureNotNull(action);

            return Subscribe(new Handler<TMsg>(action));
        }

        /// <summary>
        /// Un-Subscribes a subscription from the <see cref="MessageHub{TMsgBase}"/>.
        /// </summary>
        /// <param name="token">The token representing the subscription</param>
        public void UnSubscribe(Guid token)
        {
            EnsureNotNull(token);
            EnsureNotDisposed();

            lock (_subscriptions)
            {
                _subscriptions.RemoveAll(s => s.Token == token);
                _subscriptionRevision++;
            }
        }

        /// <summary>
        /// Checks if a subscription is active on the <see cref="MessageHub{TMsgBase}"/>.
        /// </summary>
        /// <param name="token">The token representing the subscription</param>
        /// <returns><c>True</c> if the subscription is active otherwise <c>False</c></returns>
        public bool IsSubscribed(Guid token)
        {
            EnsureNotNull(token);
            EnsureNotDisposed();

            return _subscriptions.Any(s => s.Token == token);
        }

        /// <summary>
        /// Clears all the subscriptions from the <see cref="MessageHub{TMsgBase}"/>.
        /// <remarks>The global handler and the <see cref="OnError"/> are not affected</remarks>
        /// </summary>
        public void ClearSubscriptions()
        {
            EnsureNotDisposed();

            lock (_subscriptions)
            {
                _subscriptions.Clear();
                _subscriptionRevision++;
            }
        }

        /// <summary>
        /// Disposes the <see cref="MessageHub{T}"/>.
        /// </summary>
        public void Dispose()
        {
            Interlocked.Increment(ref _disposed);
        }

        /// <summary>
        /// The method which publishes the passed in <paramref name="message"/>
        /// </summary>
        /// <param name="message">The message to publish</param>
        private void PublishImpl(TMsgBase message)
        {
            EnsureNotDisposed();

            _globalHandler(message);

            UpdateToTheLatestRevisionOfSubscriptions();

            var localSubs = _localSubscriptions.Value;
            
            // ReSharper disable once ForCanBeConvertedToForeach | Performance Critical
            for (var idx = 0; idx < localSubs.Length; idx++)
            {
                EnsureNotDisposed();

                var subscription = localSubs[idx];

                try
                {
                    subscription.Handle(message);
                }
                catch (Exception e)
                {
                    var copy = OnError;
                    copy?.Invoke(this, new MessageHubErrorEventArgs(e, subscription.Token));
                }
            }
        }

        /// <summary>
        /// Asserts that the <see cref="MessageHub{TMsgBase}"/> is not disposed.
        /// </summary>
        private void EnsureNotDisposed()
        {
            if (_disposed == 1) { throw new ObjectDisposedException(GetType().Name); }
        }

        /// <summary>
        /// Asserts that the <paramref name="obj"/> is not null.
        /// </summary>
        /// <param name="obj">The object to check for nullability</param>
        private void EnsureNotNull(object obj)
        {
            if (obj == null) { throw new NullReferenceException(nameof(obj)); }
        }

        /// <summary>
        /// Updates the publisher thread's copy of the subscriptions to the latest
        /// </summary>
        private void UpdateToTheLatestRevisionOfSubscriptions()
        {
            if (_localSubscriptionRevision.Value == _subscriptionRevision) { return; }

            lock (_subscriptions)
            {
                _localSubscriptions.Value = _subscriptions.ToArray();
                _localSubscriptionRevision.Value = _subscriptionRevision;
            }
        }

        private sealed class Subscription
        {
            public Subscription(Guid token, Handler<TMsgBase> handler)
            {
                Token = token;
                Handle = handler.Handle;
            }

            public Guid Token { get; }
            public Action<TMsgBase> Handle { get; }
        }
    }
}