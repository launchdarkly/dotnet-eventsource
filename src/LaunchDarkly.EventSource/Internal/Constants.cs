namespace LaunchDarkly.EventSource.Internal
{
    /// <summary>
    /// An internal class used to hold static values used when processing Server Sent Events.
    /// </summary>
    internal static class Constants
    {
        /// <summary>
        /// The HTTP header name for Accept.
        /// </summary>
        internal const string AcceptHttpHeader = "Accept";

        /// <summary>
        /// The HTTP header name for the last event identifier.
        /// </summary>
        internal const string LastEventIdHttpHeader = "Last-Event-ID";

        /// <summary>
        /// The HTTP header value for the Content Type.
        /// </summary>
        internal const string EventStreamContentType = "text/event-stream";

        /// <summary>
        /// The event type name for a Retry in a Server Sent Event.
        /// </summary>
        internal const string RetryField = "retry";

        /// <summary>
        /// The identifier field name in a Server Sent Event.
        /// </summary>
        internal const string IdField = "id";

        /// <summary>
        /// The event type field name in a Server Sent Event.
        /// </summary>
        internal const string EventField = "event";

        /// <summary>
        /// The data field name in a Server Sent Event.
        /// </summary>
        internal const string DataField = "data";
    }
}
