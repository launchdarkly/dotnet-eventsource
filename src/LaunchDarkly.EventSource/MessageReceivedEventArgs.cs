using System;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// Provides data recieved in the EventSource <see cref="EventSource.MessageReceived"/> event.
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public class MessageReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the <see cref="MessageEvent"/> data recieved by the Server Sent Event.
        /// </summary>
        /// <value>
        /// The <see cref="MessageEvent"/> data recieved by the Server Sent Event.
        /// </value>
        public MessageEvent Message { get; }

        /// <summary>
        /// Gets the name of the event type received by the Server Sent Event.
        /// </summary>
        /// <value>
        /// The name of the event type.
        /// </value>
        public string EventName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="message">The <see cref="MessageEvent"/> data recieved by the Server Sent Event.</param>
        /// <param name="eventName">Name of the event type received by the Server Sent Event.</param>
        public MessageReceivedEventArgs(MessageEvent message, string eventName)
        {
            Message = message;
            EventName = eventName;
        }
    }
}