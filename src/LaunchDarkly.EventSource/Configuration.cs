using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using LaunchDarkly.Logging;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// An immutable class containing configuration properties for <see cref="EventSource"/>.
    /// </summary>
    /// <seealso cref="ConfigurationBuilder"/>
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

        /// <summary>
        /// The logger name that will be used if you specified a logging implementation but did not
        /// provide a specific logger instance.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.LogAdapter(ILogAdapter)"/>
        public const string DefaultLoggerName = "EventSource";

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
        /// <seealso cref="ConfigurationBuilder.ConnectionTimeout(TimeSpan)"/>
        public TimeSpan ConnectionTimeout { get; }

        [Obsolete("Use ConnectionTimeout.")]
        public TimeSpan ConnectionTimeOut { get { return ConnectionTimeout; } }

        /// <summary>
        /// The duration to wait before attempting to reconnect to the EventSource API.
        /// </summary>
        public TimeSpan DelayRetryDuration { get; }
        /// <seealso cref="ConfigurationBuilder.DelayRetryDuration(TimeSpan)"/>

        /// <summary>
        /// The amount of time a connection must stay open before the EventSource resets its backoff delay.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.BackoffResetThreshold(TimeSpan)"/>
        public TimeSpan BackoffResetThreshold { get; }

        /// <summary>
        /// The character encoding to use when reading the stream if the server did not specify
        /// an encoding in a <c>Content-Type</c> header.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.DefaultEncoding(Encoding)"/>
        public Encoding DefaultEncoding { get; }

        /// <summary>
        /// The timeout when reading from the EventSource API.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.ReadTimeout(TimeSpan)"/>
        public TimeSpan ReadTimeout { get; }

        [Obsolete("Use ReadTimeout.")]
        public TimeSpan ReadTimeOut { get { return ReadTimeout; } }

        /// <summary>
        /// Gets the last event identifier.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.LastEventId(string)"/>
        public string LastEventId { get; }

        /// <summary>
        /// The logger to be used for all EventSource log output.
        /// </summary>
        /// <remarks>
        /// This is never null; if logging is not configured, it will be <c>LaunchDarkly.Logging.Logs.None</c>.
        /// </remarks>
        /// <seealso cref="ConfigurationBuilder.Logger(Logger)"/>
        public Logger Logger { get; }

        /// <summary>
        /// The request headers to be sent with each EventSource HTTP request.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.RequestHeader(string, string)"/>
        /// <seealso cref="ConfigurationBuilder.RequestHeaders(IDictionary{string, string})"/>
        public IDictionary<string, string> RequestHeaders { get; }

        /// <summary>
        /// The HttpMessageHandler that will be used for the HTTP client, or null for the default handler.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.MessageHandler(HttpMessageHandler)"/>
        public HttpMessageHandler MessageHandler { get; }

        /// <summary>
        /// The HttpClient that will be used as the HTTP client, or null for a new HttpClient.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.HttpClient(HttpClient)"/>
        public HttpClient HttpClient { get; }

        /// <summary>
        /// The HTTP method that will be used when connecting to the EventSource API.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.Method(HttpMethod)"/>
        public HttpMethod Method { get; }

        /// <summary>
        /// Whether to use UTF-8 byte arrays internally if possible when reading the stream.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.PreferDataAsUtf8Bytes(bool)"/>
        public bool PreferDataAsUtf8Bytes { get; }

        /// <summary>
        /// A factory for HTTP request body content, if the HTTP method is one that allows a request body.
        /// is one that allows a request body. This is in the form of a factory function because the request
        /// may need to be sent more than once.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.RequestBodyFactory(HttpContentFactory)"/>
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
        public Configuration(
            Uri uri,
            HttpMessageHandler messageHandler = null,
            TimeSpan? connectionTimeout = null,
            TimeSpan? delayRetryDuration = null,
            TimeSpan? readTimeout = null,
            IDictionary<string, string> requestHeaders = null,
            string lastEventId = null,
            Logger logger = null,
            HttpMethod method = null,
            HttpContentFactory requestBodyFactory = null,
            TimeSpan? backoffResetThreshold = null,
            HttpClient httpClient = null,
            Encoding defaultEncoding = null,
            bool preferDataAsUtf8Bytes = false
            )
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
                throw new ArgumentException(Resources.Configuration_HttpClient_With_ConnectionTimeout, nameof(connectionTimeout));
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
            Logger = logger ?? Logs.None.Logger("");
            Method = method;
            RequestBodyFactory = requestBodyFactory;
            DefaultEncoding = defaultEncoding ?? Encoding.UTF8;
            PreferDataAsUtf8Bytes = preferDataAsUtf8Bytes;
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
