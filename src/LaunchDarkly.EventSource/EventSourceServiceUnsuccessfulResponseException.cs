
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

        #endregion

        #region Public Constructors 

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="statusCode">the HTTP status code of the response</param>
        public EventSourceServiceUnsuccessfulResponseException(int statusCode) :
            base(string.Format(Resources.ErrorHttpStatus, statusCode))
        {
            StatusCode = statusCode;
        }

        #endregion

    }
}
