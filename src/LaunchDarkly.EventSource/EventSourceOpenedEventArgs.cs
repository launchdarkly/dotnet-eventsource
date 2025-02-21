using System;
using System.Collections.Generic;

namespace LaunchDarkly.EventSource
{
    internal class EventSourceOpenedEventArgs: EventArgs
    {
        public EventSourceOpenedEventArgs(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
        {
            Headers = headers;
        }
        public IEnumerable<KeyValuePair<string,IEnumerable<string>>> Headers { get; }
    }
}
