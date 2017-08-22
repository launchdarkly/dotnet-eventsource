namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// An internal class used to hold static values used when processing Server Sent Events.
    /// </summary>
    internal static class Constants
    {
        /// <summary>
        /// The HTTP header name for Accept.
        /// </summary>
        internal static string AcceptHttpHeader = "Accept";

        /// <summary>
        /// The HTTP header name for the last event identifier.
        /// </summary>
        internal static string LastEventIdHttpHeader = "Last-Event-ID";

        /// <summary>
        /// The HTTP header value for the Content Type.
        /// </summary>
        internal static string EventStreamContentType = "text/event-stream";

        /// <summary>
        /// The event type name for a Retry in a Server Sent Event.
        /// </summary>
        internal static string RetryField = "retry";

        /// <summary>
        /// The identifier field name in a Server Sent Event.
        /// </summary>
        internal static string IdField = "id";

        /// <summary>
        /// The event type field name in a Server Sent Event.
        /// </summary>
        internal static string EventField = "event";

        /// <summary>
        /// The data field name in a Server Sent Event.
        /// </summary>
        internal static string DataField = "data";

        /// <summary>
        /// The message field name in a Server Sent Event.
        /// </summary>
        internal static string MessageField = "message";
    }
}
