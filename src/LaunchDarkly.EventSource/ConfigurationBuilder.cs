using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using LaunchDarkly.Logging;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// A standard Builder pattern for constructing a <see cref="Configuration"/> instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Initialize a builder by calling <c>new ConfigurationBuilder(uri)</c> or
    /// <c>Configuration.Builder(uri)</c>. The URI is always required; all other properties
    /// are set to defaults. Use the builder's setter methods to modify any desired properties;
    /// setter methods can be chained. Then call <c>Build()</c> to construct the final immutable
    /// <c>Configuration</c>.
    /// </para>
    /// <para>
    /// All setter methods will throw <c>ArgumentException</c> if called with an invalid value,
    /// so it is never possible for <c>Build()</c> to fail.
    /// </para>
    /// </remarks>
    public class ConfigurationBuilder
    {
        #region Private Fields

        internal readonly Uri _uri;
        internal TimeSpan? _connectionTimeout = null;
        internal Encoding _defaultEncoding = Encoding.UTF8;
        internal TimeSpan _initialRetryDelay = Configuration.DefaultInitialRetryDelay;
        internal TimeSpan _backoffResetThreshold = Configuration.DefaultBackoffResetThreshold;
        internal TimeSpan _readTimeout = Configuration.DefaultReadTimeout;
        internal string _lastEventId;
        internal ILogAdapter _logAdapter;
        internal Logger _logger;
        internal IDictionary<string, string> _requestHeaders = new Dictionary<string, string>();
        internal HttpMessageHandler _httpMessageHandler;
        internal HttpClient _httpClient;
        internal TimeSpan _maxRetryDelay = Configuration.DefaultMaxRetryDelay;
        internal HttpMethod _method = HttpMethod.Get;
        internal bool _preferDataAsUtf8Bytes = false;
        internal Func<HttpContent> _requestBodyFactory;

        #endregion

        #region Constructor

        internal ConfigurationBuilder(Uri uri)
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
        public Configuration Build() =>
            new Configuration(this);

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
            _connectionTimeout = TimeSpanCanBeInfinite(connectionTimeout);
            return this;
        }

        /// <summary>
        /// Sets the character encoding to use when reading the stream if the server did not specify
        /// an encoding in a <c>Content-Type</c> header.
        /// </summary>
        /// <param name="defaultEncoding">A <c>System.Text.Encoding</c>; if null, the default
        /// is <see cref="Encoding.UTF8"/></param>
        /// <returns>the builder</returns>
        /// <seealso cref="MessageEvent"/>
        /// <seealso cref="PreferDataAsUtf8Bytes"/>
        public ConfigurationBuilder DefaultEncoding(Encoding defaultEncoding)
        {
            _defaultEncoding = defaultEncoding ?? Encoding.UTF8;
            return this;
        }

        /// <summary>
        /// Sets the initial amount of time to wait before attempting to reconnect to the EventSource API.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the connection fails more than once, the retry delay will increase from this value using
        /// a backoff algorithm.
        /// </para>
        /// <para>
        /// The default value is <see cref="Configuration.DefaultInitialRetryDelay"/>. Negative values
        /// are changed to zero.
        /// </para>
        /// <para>
        /// The actual duration of each delay will vary slightly because there is a random jitter
        /// factor to avoid clients all reconnecting at once.
        /// </para>
        /// </remarks>
        /// <param name="initialRetryDelay">the initial retry delay</param>
        /// <returns>the builder</returns>
        /// <seealso cref="MaxRetryDelay(TimeSpan)"/>
        public ConfigurationBuilder InitialRetryDelay(TimeSpan initialRetryDelay)
        {
            _initialRetryDelay = FiniteTimeSpan(initialRetryDelay);
            return this;
        }

        /// <summary>
        /// Sets the maximum amount of time to wait before attempting to reconnect.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>EventSource</c> uses an exponential backoff algorithm (with random jitter) so that
        /// the delay between reconnections starts at <see cref="InitialRetryDelay(TimeSpan)"/> but
        /// increases with each subsequent attempt. <c>MaxRetryDelay</c> sets a limit on how long
        /// the delay can be.
        /// </para>
        /// <para>
        /// The default value is <see cref="Configuration.DefaultMaxRetryDelay"/>. Negative values
        /// are changed to zero.
        /// </para>
        /// </remarks>
        /// <param name="maxRetryDelay">the maximum retry delay</param>
        /// <returns>the builder</returns>
        /// <seealso cref="InitialRetryDelay(TimeSpan)"/>
        public ConfigurationBuilder MaxRetryDelay(TimeSpan maxRetryDelay)
        {
            _maxRetryDelay = FiniteTimeSpan(maxRetryDelay);
            return this;
        }

        /// <summary>
        /// Sets the amount of time a connection must stay open before the EventSource resets its backoff delay.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If a connection fails before the threshold has elapsed, the delay before reconnecting will be greater
        /// than the last delay; if it fails after the threshold, the delay will start over at the initial minimum
        /// value. This prevents long delays from occurring on connections that are only rarely restarted.
        /// </para>
        /// <para>
        /// The default value is <see cref="Configuration.DefaultBackoffResetThreshold"/>. Negative
        /// values are changed to zero.
        /// </para>
        /// </remarks>
        /// <param name="backoffResetThreshold">the threshold time</param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder BackoffResetThreshold(TimeSpan backoffResetThreshold)
        {
            _backoffResetThreshold = FiniteTimeSpan(backoffResetThreshold);
            return this;
        }

        /// <summary>
        /// Sets the timeout when reading from the EventSource API.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The connection will be automatically dropped and restarted if the server sends no data within
        /// this interval. This prevents keeping a stale connection that may no longer be working. It is common
        /// for SSE servers to send a simple comment line (":") as a heartbeat to prevent timeouts.
        /// </para>
        /// <para>
        /// The default value is <see cref="Configuration.DefaultReadTimeout"/>.
        /// </para>
        /// </remarks>
        public ConfigurationBuilder ReadTimeout(TimeSpan readTimeout)
        {
            _readTimeout = TimeSpanCanBeInfinite(readTimeout);
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
        /// Sets the logging implementation to be used for all EventSource log output.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This uses the <c>ILogAdapter</c> abstraction from the <c>LaunchDarkly.Logging</c> library,
        /// which provides several basic implementations such as <c>Logs.ToConsole</c> and an integration
        /// with the .NET Core logging framework. For more about this and about adapters to other logging
        /// frameworks, see <a href="https://github.com/launchdarkly/dotnet-logging"><c>LaunchDarkly.Logging</c></a>.
        /// </para>
        /// <para>
        /// <c>LaunchDarkly.Logging</c> defines logging levels of Debug, Info, Warn, and Error. If you do not
        /// want detailed Debug-level logging, use the <c>Level()</c> modifier to set a minimum level of Info
        /// or above, as shown in the code example (unless you are using an adapter to another logging
        /// framework that has its own way of doing log filtering).
        /// </para>
        /// <para>
        /// Log messages will use <see cref="Configuration.DefaultLoggerName"/> as the logger name. If you
        /// want to specify a different logger name, use <see cref="Logger(Logging.Logger)"/>.
        /// </para>
        /// <para>
        /// If you don't specify <see cref="LogAdapter(ILogAdapter)"/> or <see cref="Logger(Logging.Logger)"/>,
        /// EventSource will not do any logging.
        /// </para>
        /// </remarks>
        /// <example>
        ///     using LaunchDarkly.Logging;
        ///     
        ///     // Send log output to the console (standard error), suppressing Debug messages
        ///     var config = new ConfigurationBuilder(uri).
        ///         LogAdapter(Logs.ToConsole.Level(LogLevel.Info)).
        ///         Build();
        /// </example>
        /// <param name="logAdapter">a <c>LaunchDarkly.Logging.ILogAdapter</c></param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder LogAdapter(ILogAdapter logAdapter)
        {
            _logAdapter = logAdapter;
            return this;
        }

        /// <summary>
        /// Sets a custom logger to be used for all EventSource log output.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This uses the <c>Logger</c> type from the <c>LaunchDarkly.Logging</c> library,
        /// which provides several basic implementations such as <c>Logs.ToConsole</c> and an integration
        /// with the .NET Core logging framework. For more about this and about adapters to other logging
        /// frameworks, see <a href="https://github.com/launchdarkly/dotnet-logging"><c>LaunchDarkly.Logging</c></a>.
        /// </para>
        /// <para>
        /// If you don't specify <see cref="LogAdapter(ILogAdapter)"/> or <see cref="Logger(Logging.Logger)"/>,
        /// EventSource will not do any logging.
        /// </para>
        /// </remarks>
        /// <example>
        ///     using LaunchDarkly.Logging;
        ///     
        ///     // Define a logger that sends output to the console (standard output), suppressing
        ///     // Debug messages, and using a logger name of "EventStream"
        ///     var logger = Logs.ToConsole.Level(LogLevel.Info).Logger("EventStream");
        ///     var config = new ConfigurationBuilder(uri).
        ///         Logger(logger).
        ///         Build();
        /// </example>
        /// <param name="logger">a <c>LaunchDarkly.Logging.Logger</c> instance</param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder Logger(Logger logger)
        {
            _logger = logger;
            return this;
        }

        /// <summary>
        /// Specifies whether to use UTF-8 byte arrays internally if possible when
        /// reading the stream.
        /// </summary>
        /// <remarks>
        /// As described in <see cref="MessageEvent"/>, in some applications it may be
        /// preferable to store and process event data as UTF-8 byte arrays rather than
        /// strings. By default, <c>EventSource</c> will use the <c>string</c> type when
        /// processing the event stream; if you then use <see cref="MessageEvent.DataUtf8Bytes"/>
        /// to get the data, it will be converted to a byte array as needed. It will also
        /// always use the <c>string</c> type internally if the stream's encoding is not
        /// UTF-8. However, if the stream's encoding is UTF-8 <c>and</c> you have set
        /// <c>PreferDataAsUtf8Bytes</c> to <see langword="true"/>, the event data will
        /// be stored internally as a UTF-8 byte array so that if you read
        /// <see cref="MessageEvent.DataUtf8Bytes"/>, you will get the same array with no
        /// extra copying or conversion. Therefore, for greatest efficiency you should set
        /// this to <see langword="true"/> if you intend to process the data as UTF-8 and
        /// if you expect that the server will provide it in that encoding. If the server
        /// turns out not to use that encoding, everything will still work the same except
        /// that there will be more overhead from string conversion.
        /// </remarks>
        /// <param name="preferDataAsUtf8Bytes">true if you intend to request the event
        /// data as UTF-8 bytes</param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder PreferDataAsUtf8Bytes(bool preferDataAsUtf8Bytes)
        {
            _preferDataAsUtf8Bytes = preferDataAsUtf8Bytes;
            return this;
        }

        /// <summary>
        /// Sets the request headers to be sent with each EventSource HTTP request.
        /// </summary>
        /// <param name="headers">the headers (null is equivalent to an empty dictionary)</param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder RequestHeaders(IDictionary<string, string> headers)
        {
            _requestHeaders = headers is null ? new Dictionary<string, string>() :
                new Dictionary<string, string>(headers);
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
            if (name != null)
            {
                _requestHeaders[name] = value;
            }
            return this;
        }

        /// <summary>
        /// Sets the <c>HttpMessageHandler</c> that will be used for the HTTP client, or null for the default handler.
        /// </summary>
        /// <remarks>
        /// If you have specified a custom HTTP client instance with <see cref="HttpClient"/>, then
        /// <see cref="HttpMessageHandler(HttpMessageHandler)"/> is ignored.
        /// </remarks>
        /// <param name="handler">the message handler implementation</param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder HttpMessageHandler(HttpMessageHandler handler)
        {
            this._httpMessageHandler = handler;
            return this;
        }

        /// <summary>
        /// Specifies that EventSource should use a specific HttpClient instance for HTTP requests.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Normally, EventSource creates its own HttpClient and disposes of it when you dispose of the
        /// EventSource. If you provide your own HttpClient using this method, you are responsible for
        /// managing the HttpClient's lifecycle-- EventSource will not dispose of it.
        /// </para>
        /// <para>
        /// EventSource will not modify this client's properties, so if you call <see cref="HttpMessageHandler"/>
        /// or <see cref="ConnectionTimeout"/>, those methods will be ignored.
        /// </para>
        /// </remarks>
        /// <param name="client">an HttpClient instance, or null to use the default behavior</param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder HttpClient(HttpClient client)
        {
            this._httpClient = client;
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
        public ConfigurationBuilder RequestBodyFactory(Func<HttpContent> factory)
        {
            this._requestBodyFactory = factory;
            return this;
        }

        /// <summary>
        /// Equivalent <see cref="RequestBodyFactory(Func{HttpContent})"/>, but for content
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

        #region Private methods

        // Canonicalizes the value so all negative numbers become InfiniteTimeSpan
        private static TimeSpan TimeSpanCanBeInfinite(TimeSpan t) =>
            t < TimeSpan.Zero ? Timeout.InfiniteTimeSpan : t;

        // Replaces all negative times with zero
        private static TimeSpan FiniteTimeSpan(TimeSpan t) =>
            t < TimeSpan.Zero ? TimeSpan.Zero : t;

        #endregion
    }
}
