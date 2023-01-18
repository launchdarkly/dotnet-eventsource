using System;
using System.Net;

namespace LaunchDarkly.EventSource.Exceptions
{
    /// <summary>
    /// An exception indicating that the remote server returned an HTTP error.
    /// </summary>
    /// <remarks>
    /// The SSE specification defines an HTTP error as any non-2xx status, or 204.
    /// </remarks>
    public class StreamHttpErrorException : StreamException
    {
        /// <summary>
        /// The HTTP status code.
        /// </summary>
        public HttpStatusCode Status { get; }

        /// <summary>
        /// Creates an instance.
        /// </summary>
        /// <param name="status">the HTTP status code</param>
        public StreamHttpErrorException(HttpStatusCode status) :
            base(string.Format(Resources.ErrorHttpStatus, (int)status))
        {
            Status = status;
        }

        /// <inheritdoc/>
        public override bool Equals(object o) =>
            o is StreamHttpErrorException e && Status == e.Status;

        /// <inheritdoc/>
        public override int GetHashCode() =>
            Status.GetHashCode();
    }
}
