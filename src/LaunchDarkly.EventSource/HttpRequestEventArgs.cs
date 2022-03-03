using System;
using System.Net.Http;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// Provides data recieved in the EventSource <see cref="EventSource.OnHttpRequest"/> event. 
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public class HttpRequestEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the http request.
        /// </summary>
        /// <value>
        /// The comment.
        /// </value>
        public HttpRequestMessage Request { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpRequestEventArgs"/> class.
        /// </summary>
        /// <param name="request">The related http request.</param>
        public HttpRequestEventArgs(HttpRequestMessage request)
        {
            Request = request;
        }
    }
}
