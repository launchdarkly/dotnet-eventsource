using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// A class used 
    /// </summary>
    public sealed class Configuration
    {

        #region Private Fields

        private readonly TimeSpan _defaultDelayRetryDuration = TimeSpan.FromMilliseconds(1000);
        private readonly TimeSpan _defaultConnectionTimeout = TimeSpan.FromMilliseconds(10000);
        private readonly TimeSpan _defaultReadTimeout = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _maximumRetryDuration = TimeSpan.FromMilliseconds(30000);

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
        /// The <see cref="TimeSpan"/> before the connection times out. The default value is 10,000 milliseconds (10 seconds).
        /// </value>
        public TimeSpan ConnectionTimeOut { get; }

        /// <summary>
        /// Gets the duration to wait before attempting to reconnect to the EventSource API.
        /// </summary>
        /// <value>
        /// The amount of time to wait before attempting to reconnect to the EventSource API. The default value is 1,000 milliseconds (1 second).
        /// The maximum time allowed is 30,000 milliseconds (30 seconds).
        /// </value>
        public TimeSpan DelayRetryDuration { get; }

        /// <summary>
        /// Gets the time-out when reading from the EventSource API.
        /// </summary>
        /// <value>
        /// The <see cref="TimeSpan"/> before reading times out. The default value is 300,000 milliseconds (5 minutes).
        /// </value>
        public TimeSpan ReadTimeOut { get; }

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

        /// <summary>
        /// Gets the HttpMessageHandler used to call the EventSource API.
        /// </summary>
        /// <value>
        /// The <see cref="HttpMessageHandler"/>.
        /// </value>
        public HttpMessageHandler MessageHandler { get; }
        
        /// <summary>
        /// Gets the maximum amount of time to wait before attempting to reconnect to the EventSource API. 
        /// This value is read-only and cannot be set.
        /// </summary>
        /// <value>
        /// The maximum duration of the retry.
        /// </value>
        public TimeSpan MaximumDelayRetryDuration
        {
            get
            {
                return _maximumRetryDuration;
            }
        }

        #endregion

        #region Public Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="Configuration" /> class.
        /// </summary>
        /// <param name="uri">The URI used to connect to the remote EventSource API.</param>
        /// <param name="messageHandler">The message handler to use when sending API requests. If null, the <see cref="HttpClientHandler"/> is used.</param>
        /// <param name="connectionTimeOut">The connection time out. If null, defaults to 10 seconds.</param>
        /// <param name="delayRetryDuration">The time to wait before attempting to reconnect to the EventSource API. If null, defaults to 1 second.</param>
        /// <param name="readTimeout">The time out when reading data from the EventSource API. If null, defaults to 5 minutes.</param>
        /// <param name="requestHeaders">Request headers used when connecting to the remote EventSource API.</param>
        /// <param name="lastEventId">The last event identifier.</param>
        /// <param name="logger">The logger used for logging internal messages.</param>
        /// <exception cref="ArgumentOutOfRangeException">If the delayRetryDuration value is greater than 30 seconds, an ArgumentOutOfRangeException will be thrown.</exception>
        public Configuration(Uri uri, HttpMessageHandler messageHandler = null, TimeSpan? connectionTimeOut = null, TimeSpan? delayRetryDuration = null, TimeSpan? readTimeout = null, IDictionary<string, string> requestHeaders = null, string lastEventId = null, ILogger logger = null)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            if (connectionTimeOut.HasValue && connectionTimeOut.Value != Timeout.InfiniteTimeSpan && connectionTimeOut.Value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(connectionTimeOut), Resources.Configuration_Value_Greater_Than_Zero);

            if (delayRetryDuration.HasValue && delayRetryDuration.Value > MaximumDelayRetryDuration)
                throw new ArgumentOutOfRangeException(nameof(delayRetryDuration), string.Format(Resources.Configuration_RetryDuration_Exceeded, _maximumRetryDuration.Milliseconds));

            if (readTimeout.HasValue && readTimeout.Value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(readTimeout), Resources.Configuration_Value_Greater_Than_Zero);

            Uri = uri;
            MessageHandler = messageHandler ?? new HttpClientHandler();
            ConnectionTimeOut = connectionTimeOut ?? _defaultConnectionTimeout;
            DelayRetryDuration = delayRetryDuration ?? _defaultDelayRetryDuration;
            ReadTimeOut = readTimeout ?? _defaultReadTimeout;
            RequestHeaders = requestHeaders;
            LastEventId = lastEventId;
            Logger = logger;
        }

        #endregion

    }
}
