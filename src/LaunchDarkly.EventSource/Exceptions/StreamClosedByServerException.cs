using System;

namespace LaunchDarkly.EventSource.Exceptions
{
    /// <summary>
    /// An exception indicating that the stream stopped because the server closed
    /// the connection.
    /// </summary>
    public class StreamClosedByServerException : StreamException
    {
        /// <summary>
        /// Creates an instance.
        /// </summary>
        public StreamClosedByServerException() :
            base(Resources.StreamClosedByServer) { }
    }
}
