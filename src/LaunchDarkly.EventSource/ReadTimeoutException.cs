using System;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// An exception that indicates that the configured read timeout elapsed without receiving
    /// any new data from the server.
    /// </summary>
    /// <remarks>
    /// Socket connections can fail silently, in which case an EventSource client without a read
    /// timeout would hang forever waiting for new data. A read timeout allows you to make a new
    /// stream connection in this case. The server can send periodic comment lines (":\n") to
    /// keep the client from timing out if the connection is still working.
    /// </remarks>
    public class ReadTimeoutException : Exception
    {
        /// <inheritdoc/>
        public override string Message => Resources.EventSourceService_Read_Timeout;
    }
}
