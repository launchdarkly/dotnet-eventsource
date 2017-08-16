using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// A class used 
    /// </summary>
    public sealed class Configuration
    {

        #region Private Fields

        private readonly Uri _defaultUri = new Uri("https://stream.launchdarkly.com/flags");

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the <see cref="System.Uri"/> used when connecting to an EventSource API.
        /// </summary>
        /// <value>
        /// The <see cref="System.Uri"/>.
        /// </value>
        public Uri Uri { get; }

        /// <summary>
        /// Gets the connection time out value used when connecting to the EventSource API.
        /// </summary>
        /// <value>
        /// The connection time out.
        /// </value>
        public TimeSpan ConnectionTimeOut { get; }

        /// <summary>
        /// Gets the duration to wait before attempting to reconnect to the EventSource API.
        /// </summary>
        /// <value>
        /// The duration of the retry delay.
        /// </value>
        public TimeSpan DelayRetryDuration { get; }

        /// <summary>
        /// Gets the last event identifier.
        /// </summary>
        /// <remarks>
        /// Setting the LastEventId in the constructor will add an HTTP request header named "Last-Event-ID" when connecting to the EventSource API
        /// </remarks>
        /// <value>
        /// The last event identifier.
        /// </value>
        public string LastEventId { get; }

        /// <summary>
        /// Gets the <see cref="Microsoft.Extensions.Logging.ILogger"/> used internally in the <see cref="EventSource"/> class.
        /// </summary>
        /// <value>
        /// The ILogger to use for internal logging.
        /// </value>
        public ILogger Logger { get; }

        /// <summary>
        /// Gets or sets the request headers used when connecting to the EventSource API.
        /// </summary>
        /// <value>
        /// The request headers.
        /// </value>
        public IDictionary<string, string> RequestHeaders { get; }

        public HttpMessageHandler MessageHandler { get; }

        #endregion

        #region Public Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="Configuration" /> class.
        /// </summary>
        /// <param name="uri">The URI used to connect to the remote EventSource API.</param>
        /// <param name="messageHandler">The message handler to use when sending API requests.</param>
        /// <param name="connectionTimeOut">The connection time out. If null, defaults to 10 seconds.</param>
        /// <param name="delayRetryDuration">Duration of the delay retry. if null, defaults to 1 second.</param>
        /// <param name="requestHeaders">Request headers used when connecting to the remote EventSource API.</param>
        /// <param name="lastEventId">The last event identifier.</param>
        /// <param name="logger">The logger used for logging internal messages.</param>
        public Configuration(Uri uri, HttpMessageHandler messageHandler = null, TimeSpan? connectionTimeOut = null, TimeSpan? delayRetryDuration = null, IDictionary<string, string> requestHeaders = null, string lastEventId = null, ILogger logger = null)
        {
            Uri = uri ?? _defaultUri;
            MessageHandler = messageHandler ?? new HttpClientHandler();
            ConnectionTimeOut = connectionTimeOut ?? TimeSpan.FromMilliseconds(10000);
            DelayRetryDuration = delayRetryDuration ?? TimeSpan.FromMilliseconds(1000);
            RequestHeaders = requestHeaders;
            LastEventId = lastEventId;
            Logger = logger;
        }

        #endregion

    }
}
