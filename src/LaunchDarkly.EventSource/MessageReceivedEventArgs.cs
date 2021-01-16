using System;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// The parameter type for a <see cref="EventSource.MessageReceived"/> event handler.
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public class MessageReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// A <see cref="MessageEvent"/> representing the event that was received from the
        /// SSE stream.
        /// </summary>
        public MessageEvent Message { get; }

        /// <summary>
        /// Shortcut for getting the <see cref="MessageEvent.Name"/> property of th eevent.
        /// </summary>
        public string EventName => Message.Name;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="message">the <see cref="MessageEvent"/> received from the stream</param>
        public MessageReceivedEventArgs(MessageEvent message)
        {
            Message = message;
        }
    }
}