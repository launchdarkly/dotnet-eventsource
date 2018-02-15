using System;
using System.Collections.Generic;
using System.Text;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// General superclass for exceptions that caused the EventSource to disconnect or fail to establish
    /// a connection.
    /// </summary>
    public class EventSourceServiceCancelledException : Exception
    {

        #region Public Constructors 

        public EventSourceServiceCancelledException(string message) : base(message)
        {
            
        }

        public EventSourceServiceCancelledException(string message, Exception innerException) : base(message, innerException)
        {
            
        }

        #endregion

    }
}
