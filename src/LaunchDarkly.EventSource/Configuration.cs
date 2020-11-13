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
        public static readonly TimeSpan DefaultBackoffResetThreshold = TimeSpan.FromMinutes(1);

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
        /// the connection timeout value used when connecting to the EventSource API.
        /// </summary>
        public TimeSpan ConnectionTimeout { get; }

        [Obsolete("Use ConnectionTimeout.")]
        public TimeSpan ConnectionTimeOut { get { return ConnectionTimeout; } }

        /// <summary>
        /// The duration to wait before attempting to reconnect to the EventSource API.
        /// </summary>
        public TimeSpan DelayRetryDuration { get; }

        /// <summary>
        /// The amount of time a connection must stay open before the EventSource resets its backoff delay.
        /// </summary>
        public TimeSpan BackoffResetThreshold { get; }

        /// <summary>
        /// The timeout when reading from the EventSource API.
        /// </summary>
        public TimeSpan ReadTimeout { get; }

        [Obsolete("Use ReadTimeout.")]
        public TimeSpan ReadTimeOut { get { return ReadTimeout; } }

        /// <summary>
        /// Gets the last event identifier.
        /// </summary>
        public string LastEventId { get; }

        /// <summary>
        /// A custom logger to be used for all EventSource log output.
        /// </summary>
        public ILog Logger { get; }

        /// <summary>
        /// The request headers to be sent with each EventSource HTTP request.
        /// </summary>
        public IDictionary<string, string> RequestHeaders { get; }

        /// <summary>
        /// The HttpMessageHandler that will be used for the HTTP client, or null for the default handler.
        /// </summary>
        public HttpMessageHandler MessageHandler { get; }

        /// <summary>
        /// The HttpClient that will be used as the HTTP client, or null for a new HttpClient.
        /// </summary>
        public HttpClient HttpClient { get; }

        /// <summary>
        /// The HTTP method that will be used when connecting to the EventSource API.
        /// </summary>
        public HttpMethod Method { get; }

        /// <summary>
        /// A factory for HTTP request body content, if the HTTP method is one that allows a request body.
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
        /// <param name="httpClient">The http client to be used when sending API requests. You can specify either <paramref name="messageHandler"/> or httpClient.</param>
        /// <param name="connectionTimeout">The connection timeout. If null, defaults to 10 seconds. Can not be used with <paramref name="httpClient"/></param>
        /// <param name="delayRetryDuration">The time to wait before attempting to reconnect to the EventSource API. If null, defaults to 1 second.</param>
        /// <param name="readTimeout">The timeout when reading data from the EventSource API. If null, defaults to 5 minutes.</param>
        /// <param name="requestHeaders">Request headers used when connecting to the remote EventSource API.</param>
        /// <param name="lastEventId">The last event identifier.</param>
        /// <param name="logger">The logger used for logging internal messages.</param>
        /// <param name="method">The HTTP method used to connect to the remote EventSource API.</param>
        /// <param name="requestBodyFactory">A function that produces an HTTP request body to send to the remote EventSource API.</param>
        /// <exception cref="ArgumentNullException">Throws ArgumentNullException if the uri parameter is null.</exception>
        /// <exception cref="ArgumentException">Throws ArgumentException if both httpClient and messageHandler is not null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     <p><paramref name="connectionTimeout"/> is less than zero. </p>
        ///     <p>- or - </p>
        ///     <p><paramref name="delayRetryDuration"/> is greater than 30 seconds. </p>
        ///     <p>- or - </p>
        ///     <p><paramref name="readTimeout"/> is less than zero. </p>
        /// </exception>
        public Configuration(Uri uri, HttpMessageHandler messageHandler = null, TimeSpan? connectionTimeout = null, TimeSpan? delayRetryDuration = null,
            TimeSpan? readTimeout = null, IDictionary<string, string> requestHeaders = null, string lastEventId = null, ILog logger = null,
            HttpMethod method = null, HttpContentFactory requestBodyFactory = null, TimeSpan? backoffResetThreshold = null, HttpClient httpClient = null)
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

            if (httpClient != null && messageHandler != null)
            {
                throw new ArgumentException(Resources.Configuration_HttpClient_With_MessageHandler, nameof(messageHandler));
            }
            if (httpClient != null && connectionTimeout != null)
            {
                throw new ArgumentException(Resources.Configuration_HttpClient_With_ConnectionTimeout, nameof(messageHandler));
            }

            Uri = uri;
            MessageHandler = messageHandler;
            HttpClient = httpClient;
            ConnectionTimeout = connectionTimeout ?? DefaultConnectionTimeout;
            DelayRetryDuration = delayRetryDuration ?? DefaultDelayRetryDuration;
            BackoffResetThreshold = backoffResetThreshold ?? DefaultBackoffResetThreshold;
            ReadTimeout = readTimeout ?? DefaultReadTimeout;
            RequestHeaders = requestHeaders;
            LastEventId = lastEventId;
            Logger = logger;
            Method = method;
            RequestBodyFactory = requestBodyFactory;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Provides a new <see cref="ConfigurationBuilder"/> for constructing a configuration.
        /// </summary>
        /// <param name="uri">the EventSource URI</param>
        /// <returns>a new builder instance</returns>
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
