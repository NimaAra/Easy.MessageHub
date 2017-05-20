namespace Easy.MessageHub
{
    using System;

    /// <summary>
    /// An implementation of the <c>Event Aggregator</c> pattern.
    /// </summary>
    public interface IMessageHub : IDisposable
    {
        /// <summary>
        /// Invoked if an error occurs when publishing the message to a subscriber.
        /// </summary>
        event EventHandler<MessageHubErrorEventArgs> OnError;

        /// <summary>
        /// Registers a callback which is invoked on every message published by the <see cref="IMessageHub"/>.
        /// <remarks>Invoking this method with a new <paramref name="onMessage"/>overwrites the previous one.</remarks>
        /// </summary>
        /// <param name="onMessage">
        /// The callback to invoke on every message
        /// <remarks>The callback receives the type of the message and the message as arguments</remarks>
        /// </param>
        void RegisterGlobalHandler(Action<Type, object> onMessage);

        /// <summary>
        /// Publishes the <paramref name="message"/> on the <see cref="IMessageHub"/>.
        /// </summary>
        /// <param name="message">The message to published</param>
        void Publish<T>(T message);

        /// <summary>
        /// Subscribes a callback against the <see cref="IMessageHub"/> for a specific type of message.
        /// </summary>
        /// <typeparam name="T">The type of message to subscribe to</typeparam>
        /// <param name="action">The callback to be invoked once the message is published on the <see cref="IMessageHub"/></param>
        /// <returns>The token representing the subscription</returns>
        Guid Subscribe<T>(Action<T> action);

        /// <summary>
        /// Subscribes a callback against the <see cref="MessageHub"/> for a specific type of message.
        /// </summary>
        /// <typeparam name="T">The type of message to subscribe to</typeparam>
        /// <param name="action">The callback to be invoked once the message is published on the <see cref="MessageHub"/></param>
        /// <param name="throttleBy">The <see cref="TimeSpan"/> specifying the rate at which subscription is throttled</param>
        /// <returns>The token representing the subscription</returns>
        Guid Subscribe<T>(Action<T> action, TimeSpan throttleBy);

        /// <summary>
        /// Un-Subscribes a subscription from the <see cref="IMessageHub"/>.
        /// </summary>
        /// <param name="token">The token representing the subscription</param>
        void UnSubscribe(Guid token);

        /// <summary>
        /// Checks if a specific subscription is active on the <see cref="IMessageHub"/>.
        /// </summary>
        /// <param name="token">The token representing the subscription</param>
        /// <returns><c>True</c> if the subscription is active otherwise <c>False</c></returns>
        bool IsSubscribed(Guid token);

        /// <summary>
        /// Clears all the subscriptions from the <see cref="IMessageHub"/>.
        /// <remarks>The global handler and the <see cref="OnError"/> are not affected</remarks>
        /// </summary>
        void ClearSubscriptions();
    }
}