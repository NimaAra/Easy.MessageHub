namespace Easy.MessageHub
{
    using System;
    using System.Diagnostics;
    using System.Threading;
#if NET_STANDARD
    using System.Reflection;
#endif   

    /// <summary>
    /// An implementation of the <c>Event Aggregator</c> pattern.
    /// </summary>
    public sealed class MessageHub : IMessageHub
    {
    #region Singleton
        // ReSharper disable once InconsistentNaming
        private static readonly MessageHub _instance = new MessageHub();
        static MessageHub() { } // Empty static constructor - forces laziness
        private MessageHub() { }
    #endregion


        private Action<Type, object> _globalHandler;
        private int _disposed;

        /// <summary>
        /// Returns a single instance of the <see cref="MessageHub"/>
        /// </summary>
        // ReSharper disable once ConvertToAutoProperty
        public static MessageHub Instance => _instance;

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
            var localSubscriptions = Subscriptions.GetTheLatestRevisionOfSubscriptions();

            var msgType = typeof(T);

#if NET_STANDARD
            var msgTypeInfo = msgType.GetTypeInfo();
#endif
            _globalHandler?.Invoke(msgType, message);

            // ReSharper disable once ForCanBeConvertedToForeach | Performance Critical
            for (var idx = 0; idx < localSubscriptions.Length; idx++)
            {
                var subscription = localSubscriptions[idx];

#if NET_STANDARD
                if (!subscription.Type.GetTypeInfo().IsAssignableFrom(msgTypeInfo)) { continue; }
#else
                if (!subscription.Type.IsAssignableFrom(msgType)) { continue; }
#endif
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
        /// Subscribes a callback against the <see cref="MessageHub"/> for a specific type of message.
        /// </summary>
        /// <typeparam name="T">The type of message to subscribe to</typeparam>
        /// <param name="action">The callback to be invoked once the message is published on the <see cref="MessageHub"/></param>
        /// <returns>The token representing the subscription</returns>
        public Guid Subscribe<T>(Action<T> action)
        {
            return Subscribe(action, TimeSpan.Zero);
        }

        /// <summary>
        /// Subscribes a callback against the <see cref="MessageHub"/> for a specific type of message.
        /// </summary>
        /// <typeparam name="T">The type of message to subscribe to</typeparam>
        /// <param name="action">The callback to be invoked once the message is published on the <see cref="MessageHub"/></param>
        /// <param name="throttleBy">The <see cref="TimeSpan"/> specifying the rate at which subscription is throttled</param>
        /// <returns>The token representing the subscription</returns>
        public Guid Subscribe<T>(Action<T> action, TimeSpan throttleBy)
        {
            EnsureNotNull(action);
            EnsureNotDisposed();

            return Subscriptions.Register(throttleBy, action);
        }

        /// <summary>
        /// Un-Subscribes a subscription from the <see cref="MessageHub"/>.
        /// </summary>
        /// <param name="token">The token representing the subscription</param>
        public void UnSubscribe(Guid token)
        {
            EnsureNotDisposed();
            Subscriptions.UnRegister(token);
        }

        /// <summary>
        /// Checks if a specific subscription is active on the <see cref="MessageHub"/>.
        /// </summary>
        /// <param name="token">The token representing the subscription</param>
        /// <returns><c>True</c> if the subscription is active otherwise <c>False</c></returns>
        public bool IsSubscribed(Guid token)
        {
            EnsureNotDisposed();
            return Subscriptions.IsRegistered(token);
        }

        /// <summary>
        /// Clears all the subscriptions from the <see cref="MessageHub"/>.
        /// <remarks>The global handler and the <see cref="OnError"/> are not affected</remarks>
        /// </summary>
        public void ClearSubscriptions()
        {
            EnsureNotDisposed();
            Subscriptions.Clear();
        }

        /// <summary>
        /// Disposes the <see cref="MessageHub"/>.
        /// </summary>
        public void Dispose()
        {
            Interlocked.Increment(ref _disposed);
            Subscriptions.Dispose();
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
    }
}
