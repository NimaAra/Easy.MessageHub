namespace EasyMessageHub
{
    using System;

    /// <summary>
    /// An implementation of the <c>Event Aggregator</c> pattern.
    /// </summary>
    /// <typeparam name="TMsgBase">The type of the message to be published</typeparam>
    public interface IMessageHub<TMsgBase> : IDisposable
    {
        /// <summary>
        /// Invoked if an error occurs when publishing the message to a subscriber.
        /// </summary>
        event EventHandler<MessageHubErrorEventArgs> OnError;

        /// <summary>
        /// Registers a callback which is invoked on every message published by the <see cref="MessageHub{TMsgBase}"/>
        /// <remarks>Invoking this method with a new <paramref name="onMessage"/>overwrites the previous one.</remarks>
        /// </summary>
        /// <param name="onMessage">The callback to invoke on every message</param>
        void RegisterGlobalHandler(Action<TMsgBase> onMessage);

        /// <summary>
        /// Publishes the <paramref name="message"/>.
        /// </summary>
        /// <param name="message">The message to be published.</param>
        void Publish(TMsgBase message);

        /// <summary>
        /// Subscribes a handler against the <see cref="MessageHub{TMsgBase}"/> for a certain type of message.
        /// </summary>
        /// <typeparam name="TMsg">The type of message to subscribe to.</typeparam>
        /// <param name="handler">The handler to be invoked once the message is published by the <see cref="MessageHub{TMsgBase}"/></param>
        /// <returns>The token representing the subscription</returns>
        Guid Subscribe<TMsg>(Handler<TMsg> handler) where TMsg : TMsgBase;

        /// <summary>
        /// Subscribes a callback and a predicate against the <see cref="MessageHub{TMsgBase}"/> for a certain type of message.
        /// </summary>
        /// <typeparam name="TMsg">The type of message to subscribe to.</typeparam>
        /// <param name="action">The callback to be invoked once the message is published by the <see cref="MessageHub{TMsgBase}"/></param>
        /// <param name="predicate">The predicate which indicates whether the message should be published to the subscriber or not</param>
        /// <returns>The token representing the subscription</returns>
        Guid Subscribe<TMsg>(Action<TMsg> action, Predicate<TMsg> predicate = null) where TMsg : TMsgBase;

        /// <summary>
        /// Un-Subscribes a subscription from the <see cref="MessageHub{TMsgBase}"/>.
        /// </summary>
        /// <param name="token">The token representing the subscription</param>
        void UnSubscribe(Guid token);

        /// <summary>
        /// Checks if a subscription is active on the <see cref="MessageHub{TMsgBase}"/>.
        /// </summary>
        /// <param name="token">The token representing the subscription</param>
        /// <returns><c>True</c> if the subscription is active otherwise <c>False</c></returns>
        bool IsSubscribed(Guid token);

        /// <summary>
        /// Clears all the subscriptions from the <see cref="MessageHub{TMsgBase}"/>.
        /// <remarks>The global handler and the <see cref="OnError"/> are not affected</remarks>
        /// </summary>
        void ClearSubscriptions();
    }
}