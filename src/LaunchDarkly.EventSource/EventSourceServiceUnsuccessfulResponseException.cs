using System.Collections.Generic;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// Indicates that the EventSource was able to establish an HTTP connection, but received a
    /// non-successful status code.
    /// </summary>
    public class EventSourceServiceUnsuccessfulResponseException : EventSourceServiceCancelledException
    {
        #region Public Properties

        /// <summary>
        /// The HTTP status code of the response.
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// The HTTP headers from the response.
        /// </summary>
        public IEnumerable<KeyValuePair<string, IEnumerable<string>>> Headers { get; }

        #endregion

        #region Public Constructors 

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="statusCode">the HTTP status code of the response</param>
        public EventSourceServiceUnsuccessfulResponseException(int statusCode) :
            this(statusCode, new Dictionary<string, IEnumerable<string>>())
        {
        }

        /// <summary>
        /// Creates a new instance with headers.
        /// </summary>
        /// <param name="statusCode">the HTTP status code of the response</param>
        /// <param name="headers">the HTTP headers from the response</param>
        public EventSourceServiceUnsuccessfulResponseException(int statusCode, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers) :
            base(string.Format(Resources.ErrorHttpStatus, statusCode))
        {
            StatusCode = statusCode;
            Headers = headers ?? new Dictionary<string, IEnumerable<string>>();
        }

        #endregion

    }
}
