using System;
using System.Collections.Generic;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// Provides data for the state of the <see cref="EventSource"/> connection.
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public class StateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the state of the EventSource connection.
        /// </summary>
        /// <value>
        /// One of the <see cref="EventSource.ReadyState"/> values, which represents the state of the EventSource connection.
        /// </value>
        public ReadyState ReadyState { get; }

        /// <summary>
        /// Get the response headers. Only populated for <see cref="ReadyState.Open"/>.
        /// <value>
        /// A collection of header values when the ReadyState is Open, or null.
        /// </value>
        /// </summary>
        public IEnumerable<KeyValuePair<string,IEnumerable<string>>> Headers { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StateChangedEventArgs"/> class.
        /// </summary>
        /// <param name="readyState">One of the <see cref="EventSource.ReadyState"/> values, which represents the state of the EventSource connection.</param>
        /// <param name="headers">Response headers when the <see cref="StateChangedEventArgs.ReadyState"/> is <see cref="ReadyState.Open"/>. Otherwise null.</param>
        public StateChangedEventArgs(ReadyState readyState, IEnumerable<KeyValuePair<string,IEnumerable<string>>> headers)
        {
            ReadyState = readyState;
            Headers = headers;
        }
    }
}
