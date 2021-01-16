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

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="message">the exception message</param>
        public EventSourceServiceCancelledException(string message) : base(message) { }

        /// <summary>
        /// Creates a new instance with an inner exception.
        /// </summary>
        /// <param name="message">the exception message</param>
        /// <param name="innerException">the inner exception</param>
        public EventSourceServiceCancelledException(string message, Exception innerException) :
            base(message, innerException)
        { }

        #endregion

    }
}
