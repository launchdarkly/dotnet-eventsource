using System;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// Indicates that the EventSource was able to establish an HTTP connection, but received a
    /// non-successful status code.
    /// </summary>
    public class EventSourceServiceUnsuccessfulResponseException : EventSourceServiceCancelledException
    {
        #region Public Properties

        public int StatusCode
        {
            get;
            private set;
        }

        #endregion

        #region Public Constructors 

        public EventSourceServiceUnsuccessfulResponseException(string message, int statusCode) : base(message)
        {
            StatusCode = statusCode;
        }

        #endregion

    }
}
