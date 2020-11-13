using Common.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// A standard Builder pattern for constructing a <see cref="Configuration"/> instance.
    /// 
    /// Initialize a builder by calling <c>new ConfigurationBuilder(uri)</c> or
    /// <c>Configuration.Builder(uri)</c>. The URI is always required; all other properties
    /// are set to defaults. Use the builder's setter methods to modify any desired properties;
    /// setter methods can be chained. Then call <c>Build()</c> to construct the final immutable
    /// <c>Configuration</c>.
    /// 
    /// All setter methods will throw <c>ArgumentException</c> if called with an invalid value,
    /// so it is never possible for <c>Build()</c> to fail.
    /// </summary>
    public class ConfigurationBuilder
    {
        #region Private Fields

        private readonly Uri _uri;
        private TimeSpan? _connectionTimeout = Configuration.DefaultConnectionTimeout;
        private TimeSpan _delayRetryDuration = Configuration.DefaultDelayRetryDuration;
        private TimeSpan _backoffResetThreshold = Configuration.DefaultBackoffResetThreshold;
        private TimeSpan _readTimeout = Configuration.DefaultReadTimeout;
        private string _lastEventId;
        private ILog _logger;
        private IDictionary<string, string> _requestHeaders = new Dictionary<string, string>();
        private HttpMessageHandler _messageHandler;
        private HttpClient _httpClient;
        private HttpMethod _method = HttpMethod.Get;
        private Configuration.HttpContentFactory _requestBodyFactory;

        #endregion

        #region Constructor

        public ConfigurationBuilder(Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }
            this._uri = uri;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Constructs a <see cref="Configuration"/> instance based on the current builder properies.
        /// </summary>
        /// <returns>the configuration</returns>
        public Configuration Build()
        {
            return new Configuration(_uri, _messageHandler, _connectionTimeout, _delayRetryDuration, _readTimeout,
                _requestHeaders, _lastEventId, _logger, _method, _requestBodyFactory, httpClient:_httpClient);
        }

        /// <summary>
        /// Sets the connection timeout value used when connecting to the EventSource API.
        /// </summary>
        /// <remarks>
        /// The default value is <see cref="Configuration.DefaultConnectionTimeout"/>.
        /// </remarks>
        /// <param name="connectionTimeout">the timeout</param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder ConnectionTimeout(TimeSpan connectionTimeout)
        {
            Configuration.CheckConnectionTimeout(connectionTimeout);
            _connectionTimeout = connectionTimeout;
            return this;
        }

        /// <summary>
        /// Sets the initial amount of time to wait before attempting to reconnect to the EventSource API.
        /// </summary>
        /// <remarks>
        /// If the connection fails more than once, the retry delay will increase from this value using
        /// a backoff algorithm.
        /// 
        /// The default value is <see cref="Configuration.DefaultDelayRetryDuration"/>. The maximum
        /// allowed value is <see cref="Configuration.MaximumRetryDuration"/>.
        /// </remarks>
        /// <param name="delayRetryDuration">the initial retry delay</param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder DelayRetryDuration(TimeSpan delayRetryDuration)
        {
            Configuration.CheckDelayRetryDuration(delayRetryDuration);
            _delayRetryDuration = delayRetryDuration;
            return this;
        }

        /// <summary>
        /// Sets the amount of time a connection must stay open before the EventSource resets its backoff delay.
        /// </summary>
        /// <remarks>
        /// If a connection fails before the threshold has elapsed, the delay before reconnecting will be greater
        /// than the last delay; if it fails after the threshold, the delay will start over at the initial minimum
        /// value. This prevents long delays from occurring on connections that are only rarely restarted.
        /// 
        /// The default value is <see cref="Configuration.DefaultBackoffResetThreshold"/>.
        /// </remarks>
        /// <param name="backoffResetThreshold">the threshold time</param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder BackoffResetThreshold(TimeSpan backoffResetThreshold)
        {
            _backoffResetThreshold = backoffResetThreshold;
            return this;
        }

        /// <summary>
        /// Sets the timeout when reading from the EventSource API.
        /// </summary>
        /// <remarks>
        /// The connection will be automatically dropped and restarted if the server sends no data within
        /// this interval. This prevents keeping a stale connection that may no longer be working. It is common
        /// for SSE servers to send a simple comment line (":") as a heartbeat to prevent timeouts.
        /// 
        /// The default value is <see cref="Configuration.DefaultReadTimeout"/>.
        /// </remarks>
        public ConfigurationBuilder ReadTimeout(TimeSpan readTimeout)
        {
            Configuration.CheckReadTimeout(readTimeout);
            _readTimeout = readTimeout;
            return this;
        }

        /// <summary>
        /// Sets the last event identifier.
        /// </summary>
        /// <remarks>
        /// Setting this value will cause EventSource to add a "Last-Event-ID" header in its HTTP request.
        /// This normally corresponds to the <see cref="MessageEvent.LastEventId"/> field of a previously
        /// received event.
        /// </remarks>
        /// <param name="lastEventId">the event identifier</param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder LastEventId(string lastEventId)
        {
            _lastEventId = lastEventId;
            return this;
        }

        /// <summary>
        /// Sets a custom logger to be used for all EventSource log output.
        /// </summary>
        /// <remarks>
        /// By default, EventSource will call <see cref="LogManager.GetLogger(Type)"/> to creates its
        /// own logger.
        /// </remarks>
        /// <param name="logger">a logger instance</param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder Logger(ILog logger)
        {
            _logger = logger;
            return this;
        }

        /// <summary>
        /// Sets the request headers to be sent with each EventSource HTTP request.
        /// </summary>
        /// <param name="headers">the headers (must not be null)</param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder RequestHeaders(IDictionary<string, string> headers)
        {
            _requestHeaders = headers ?? throw new ArgumentNullException(nameof(headers));
            return this;
        }
        
        /// <summary>
        /// Adds a request header to be sent with each EventSource HTTP request.
        /// </summary>
        /// <param name="name">the header name</param>
        /// <param name="value">the header value </param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder RequestHeader(string name, string value)
        {
            _requestHeaders[name] = value;
            return this;
        }

        /// <summary>
        /// Sets the HttpMessageHandler that will be used for the HTTP client, or null for the default handler.
        /// </summary>
        /// <param name="handler">the message handler implementation</param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder MessageHandler(HttpMessageHandler handler)
        {
            this._messageHandler = handler;
            return this;
        }

        /// <summary>
        /// Sets the HttpClient that will be used for the API Calls, or null for a new HttpClient.
        /// </summary>
        /// <remarks>
        /// Setting this, you have to take care of disposing the httpClient and the connection timeout (httpClient.Timeout) yourself.
        /// </remarks>
        /// <param name="client">the httpClient</param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder HttpClient(HttpClient client)
        {
            this._httpClient = client;
            this._connectionTimeout = null;
            return this;
        }

        /// <summary>
        /// Sets the HTTP method that will be used when connecting to the EventSource API.
        /// </summary>
        /// <remarks>
        /// By default, this is <see cref="HttpMethod.Get"/>.
        /// </remarks>
        public ConfigurationBuilder Method(HttpMethod method)
        {
            this._method = method ?? throw new ArgumentNullException(nameof(method));
            return this;
        }

        /// <summary>
        /// Sets a factory for HTTP request body content, if the HTTP method is one that allows a request body.
        /// </summary>
        /// <remarks>
        /// This is in the form of a factory function because the request may need to be sent more than once.
        /// </remarks>
        /// <param name="factory">the factory function, or null for none</param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder RequestBodyFactory(Configuration.HttpContentFactory factory)
        {
            this._requestBodyFactory = factory;
            return this;
        }

        /// <summary>
        /// Equivalent <see cref="RequestBodyFactory(Configuration.HttpContentFactory)"/>, but for content
        /// that is a simple string.
        /// </summary>
        /// <param name="bodyString">the content</param>
        /// <param name="contentType">the Content-Type header</param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder RequestBody(string bodyString, string contentType)
        {
            return RequestBodyFactory(() => new StringContent(bodyString, Encoding.UTF8, contentType));
        }

        #endregion

    }
}
