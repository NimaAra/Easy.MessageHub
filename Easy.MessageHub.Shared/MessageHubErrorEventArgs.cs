namespace Easy.MessageHub
{
    using System;

    /// <summary>
    /// A class representing an error event raised by the <see cref="IMessageHub"/>
    /// </summary>
    public sealed class MessageHubErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Creates an instance of the <see cref="MessageHubErrorEventArgs"/>
        /// </summary>
        /// <param name="e">The exception thrown by the <see cref="IMessageHub"/></param>
        /// <param name="token">
        /// The subscription token of the subscriber to which 
        /// message was published by the <see cref="IMessageHub"/>
        /// </param>
        public MessageHubErrorEventArgs(Exception e, Guid token)
        {
            Exception = e;
            Token = token;
        }

        /// <summary>
        /// Gets the exception thrown by the <see cref="IMessageHub"/>
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Gets the subscription token of the subscriber to which 
        /// message was published by the <see cref="IMessageHub"/>
        /// </summary>
        public Guid Token { get; }
    }
}