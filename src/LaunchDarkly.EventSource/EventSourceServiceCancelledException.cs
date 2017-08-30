using System;
using System.Collections.Generic;
using System.Text;

namespace LaunchDarkly.EventSource
{
    internal class EventSourceServiceCancelledException : Exception
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
