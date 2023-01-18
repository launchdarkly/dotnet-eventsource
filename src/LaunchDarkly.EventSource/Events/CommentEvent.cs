using System;
namespace LaunchDarkly.EventSource.Events
{
    /// <summary>
    /// Describes a comment line received from the stream.
    /// </summary>
    /// <remarks>
    /// An SSE comment is a line that starts with a colon. There is no defined meaning for this
    /// in the SSE specification, and most clients ignore it. It may be used to provide a
    /// periodic heartbeat from the server to keep connections from timing out.
    /// </remarks>
    public class CommentEvent : IEvent
    {
        /// <summary>
        /// The comment text, not including the leading colon.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Creates an instance.
        /// </summary>
        /// <param name="text">the comment text, not including the leading colon</param>
        public CommentEvent(string text) { Text = text; }

        /// <inheritdoc/>
        public override bool Equals(object o) =>
            o is CommentEvent oc && Text == oc.Text;

        /// <inheritdoc/>
        public override int GetHashCode() =>
            Text?.GetHashCode() ?? 0;

        /// <inheritdoc/>
        public override string ToString() =>
            string.Format("CommentEvent({0})", Text);
    }
}
