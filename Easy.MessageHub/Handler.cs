namespace Easy.MessageHub
{
    using System;

    /// <summary>
    /// A class to represent handling of an event.
    /// </summary>
    /// <typeparam name="T">The type of the event to handle</typeparam>
    public class Handler<T>
    {
        private readonly Action<T> _onMessage;
        private readonly Predicate<T> _predicate;

        /// <summary>
        /// Creates an instance of the <see cref="Handler{T}"/>
        /// </summary>
        /// <param name="onMessage">The action to be invoked upon receiving of a message</param>
        public Handler(Action<T> onMessage) : this(onMessage, m => true) { }

        /// <summary>
        /// Creates an instance of the <see cref="Handler{T}"/>
        /// </summary>
        /// <param name="onMessage">The action to be invoked upon receiving of a message</param>
        /// <param name="predicate">
        /// The predicate to be evaluated before <paramref name="onMessage"/>
        /// <c>True</c> to include the message <c>False</c> to exclude (filter) the message
        /// </param>
        public Handler(Action<T> onMessage, Predicate<T> predicate)
        {
            if (onMessage == null)
            {
                throw new ArgumentNullException(nameof(onMessage), "Action cannot be null");
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate), "Predicate cannot be null");
            }

            _onMessage = onMessage;
            _predicate = predicate;
        }

        /// <summary>
        /// Handles the given <paramref name="message"/>
        /// </summary>
        /// <param name="message">The message to be handled</param>
        public void Handle(T message)
        {
            if (!_predicate(message)) { return; }
            _onMessage(message);
        }
    }
}