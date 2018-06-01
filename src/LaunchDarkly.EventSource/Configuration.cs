using Common.Logging;
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
        #region Types

        public delegate HttpContent HttpContentFactory();

        #endregion

        #region Constants

        public static readonly TimeSpan DefaultDelayRetryDuration = TimeSpan.FromMilliseconds(1000);
        public static readonly TimeSpan DefaultConnectionTimeout = TimeSpan.FromMilliseconds(10000);
        public static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan MaximumRetryDuration = TimeSpan.FromMilliseconds(30000);

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
        /// Gets the connection timeout value used when connecting to the EventSource API.
        /// </summary>
        /// <value>
        /// The <see cref="TimeSpan"/> before the connection times out. The default value is 10,000 milliseconds (10 seconds).
        /// </value>
        public TimeSpan ConnectionTimeout { get; }

        [Obsolete("Use ConnectionTimeout.")]
        public TimeSpan ConnectionTimeOut { get { return ConnectionTimeout; } }

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
        public TimeSpan ReadTimeout { get; }

        [Obsolete("Use ReadTimeout.")]
        public TimeSpan ReadTimeOut { get { return ReadTimeout; } }

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
        /// Gets the <see cref="Common.Logging.ILog"/> used internally in the <see cref="EventSource"/> class.
        /// </summary>
        /// <value>
        /// The ILog to use for internal logging.
        /// </value>
        public ILog Logger { get; }

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
        /// Gets the HTTP method that will be used when connecting to the EventSource API.
        /// Defaults to GET if not specified.
        /// </summary>
        public HttpMethod Method { get; }

        /// <summary>
        /// Gets the request body that will be sent when connecting to the EventSource API, if the HTTP method
        /// is one that allows a request body. This is in the form of a factory function because the request
        /// may need to be sent more than once.
        /// </summary>
        public HttpContentFactory RequestBodyFactory { get; }

        /// <summary>
        /// Gets the maximum amount of time to wait before attempting to reconnect to the EventSource API. 
        /// This value is read-only and cannot be set.
        /// </summary>
        /// <value>
        /// The maximum duration of the retry.
        /// </value>
        [Obsolete("Use constant MaximumRetryDuration instead.")]
        public TimeSpan MaximumDelayRetryDuration
        {
            get
            {
                return MaximumRetryDuration;
            }
        }

        #endregion

        #region Public Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="Configuration" /> class.
        /// </summary>
        /// <param name="uri">The URI used to connect to the remote EventSource API.</param>
        /// <param name="messageHandler">The message handler to use when sending API requests. If null, the <see cref="HttpClientHandler"/> is used.</param>
        /// <param name="connectionTimeOut">The connection timeout. If null, defaults to 10 seconds.</param>
        /// <param name="delayRetryDuration">The time to wait before attempting to reconnect to the EventSource API. If null, defaults to 1 second.</param>
        /// <param name="readTimeout">The timeout when reading data from the EventSource API. If null, defaults to 5 minutes.</param>
        /// <param name="requestHeaders">Request headers used when connecting to the remote EventSource API.</param>
        /// <param name="lastEventId">The last event identifier.</param>
        /// <param name="logger">The logger used for logging internal messages.</param>
        /// <param name="method">The HTTP method used to connect to the remote EventSource API.</param>
        /// <param name="requestBodyFactory">A function that produces an HTTP request body to send to the remote EventSource API.</param>
        /// <exception cref="ArgumentNullException">Throws ArgumentNullException if the uri parameter is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     <p><paramref name="connectionTimeOut"/> is less than zero. </p>
        ///     <p>- or - </p>
        ///     <p><paramref name="delayRetryDuration"/> is greater than 30 seconds. </p>
        ///     <p>- or - </p>
        ///     <p><paramref name="readTimeout"/> is less than zero. </p>
        /// </exception>
        public Configuration(Uri uri, HttpMessageHandler messageHandler = null, TimeSpan? connectionTimeout = null, TimeSpan? delayRetryDuration = null, TimeSpan? readTimeout = null, IDictionary<string, string> requestHeaders = null, string lastEventId = null, ILog logger = null,
            HttpMethod method = null, HttpContentFactory requestBodyFactory = null)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (connectionTimeout.HasValue)
            {
                CheckConnectionTimeout(connectionTimeout.Value);
            }
            if (delayRetryDuration.HasValue)
            {
                CheckDelayRetryDuration(delayRetryDuration.Value);
            }
            if (readTimeout.HasValue)
            {
                CheckReadTimeout(readTimeout.Value);
            }

            Uri = uri;
            MessageHandler = messageHandler ?? new HttpClientHandler();
            ConnectionTimeout = connectionTimeout ?? DefaultConnectionTimeout;
            DelayRetryDuration = delayRetryDuration ?? DefaultDelayRetryDuration;
            ReadTimeout = readTimeout ?? DefaultReadTimeout;
            RequestHeaders = requestHeaders;
            LastEventId = lastEventId;
            Logger = logger;
            Method = method;
            RequestBodyFactory = requestBodyFactory;
        }

        #endregion

        #region Public Methods

        public static ConfigurationBuilder Builder(Uri uri)
        {
            return new ConfigurationBuilder(uri);
        }

        #endregion

        #region Internal Methods

        internal static void CheckConnectionTimeout(TimeSpan connectionTimeout)
        {
            if (connectionTimeout != Timeout.InfiniteTimeSpan && connectionTimeout < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(connectionTimeout), Resources.Configuration_Value_Greater_Than_Zero);
            }
        }

        internal static void CheckDelayRetryDuration(TimeSpan delayRetryDuration)
        {
            if (delayRetryDuration > MaximumRetryDuration)
            {
                throw new ArgumentOutOfRangeException(nameof(delayRetryDuration), string.Format(Resources.Configuration_RetryDuration_Exceeded, MaximumRetryDuration.Milliseconds));
            }
            if (delayRetryDuration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delayRetryDuration), Resources.Configuration_Value_Greater_Than_Zero);
            }
        }

        internal static void CheckReadTimeout(TimeSpan readTimeout)
        {
            if (readTimeout < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(readTimeout), Resources.Configuration_Value_Greater_Than_Zero);
            }
        }

        #endregion
    }
}
