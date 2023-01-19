using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text;

namespace LaunchDarkly.EventSource.Exceptions
{
    /// <summary>
    /// An exception indicating that the server returned a response with an
    /// invalid content type and/or content encoding.
    /// </summary>
    /// <remarks>
    /// The SSE specification requires that all stream responses have a content
    /// type of "text/event-stream" and an encoding of UTF-8.
    /// </remarks>
    public class StreamContentException : Exception
    {
        /// <summary>
        /// The content type of the response.
        /// </summary>
        public MediaTypeHeaderValue ContentType { get; private set; }

        /// <summary>
        /// The content encoding of the response.
        /// </summary>
        public Encoding ContentEncoding { get; private set; }

        /// <summary>
        /// Creates an instance.
        /// </summary>
        /// <param name="contentType">the content type</param>
        /// <param name="contentEncoding">the content encoding</param>
        public StreamContentException(
            MediaTypeHeaderValue contentType,
            Encoding contentEncoding
            ) : base(string.Format(Resources.ErrorWrongContentTypeOrEncoding,
                    contentType, contentEncoding))
        {
            ContentType = contentType;
            ContentEncoding = contentEncoding;
        }

        /// <inheritdoc/>
        public override bool Equals(object o) =>
            o is StreamContentException e &&
            object.Equals(ContentType, e.ContentType) &&
            object.Equals(ContentEncoding, e.ContentEncoding);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            ContentType?.GetHashCode() ?? 0 * 17 +
            ContentEncoding?.GetHashCode() ?? 0;
    }
}
