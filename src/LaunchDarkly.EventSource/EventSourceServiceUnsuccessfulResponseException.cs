using System;

namespace LaunchDarkly.EventSource
{
    internal class EventSourceServiceUnsuccessfulResponseException : EventSourceServiceCancelledException
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
