using System;

namespace LaunchDarkly.EventSource.Background
{
    /// <summary>
    /// Parameter type for the <see cref="BackgroundEventSource.CommentReceived"/> event.
    /// </summary>
    public class CommentReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the comment received in the Server Sent Event.
        /// </summary>
        /// <value>
        /// The comment.
        /// </value>
        public string Comment { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommentReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="comment">The comment received in the Server Sent Event.</param>
        public CommentReceivedEventArgs(string comment)
        {
            Comment = comment;
        }
    }
}
