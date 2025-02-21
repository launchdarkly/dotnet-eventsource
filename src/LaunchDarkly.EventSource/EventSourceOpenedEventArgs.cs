using System;
using System.Collections.Generic;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// Event arguments for the EventSourceService opened event.
    /// </summary>
    internal class EventSourceOpenedEventArgs: EventArgs
    {
        /// <summary>
        /// Construct new event arguments.
        /// </summary>
        /// <param name="headers">headers from the HTTP response</param>
        public EventSourceOpenedEventArgs(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
        {
            Headers = headers;
        }

        /// <summary>
        /// Response headers from the underlying HTTP request.
        /// </summary>
        public IEnumerable<KeyValuePair<string,IEnumerable<string>>> Headers { get; }
    }
}
