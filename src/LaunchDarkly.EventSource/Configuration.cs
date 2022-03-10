using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using LaunchDarkly.Logging;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// An immutable class containing configuration properties for <see cref="EventSource"/>.
    /// </summary>
    /// <seealso cref="EventSource.EventSource(Configuration)"/>
    /// <seealso cref="ConfigurationBuilder"/>
    public sealed class Configuration
    {
        #region Constants

        /// <summary>
        /// The default value for <see cref="ConfigurationBuilder.InitialRetryDelay(TimeSpan)"/>:
        /// one second.
        /// </summary>
        public static readonly TimeSpan DefaultInitialRetryDelay = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The default value for <see cref="ConfigurationBuilder.MaxRetryDelay(TimeSpan)"/>:
        /// 30 seconds.
        /// </summary>
        public static readonly TimeSpan DefaultMaxRetryDelay = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The default value for <see cref="ConfigurationBuilder.ResponseStartTimeout(TimeSpan)"/>:
        /// 10 seconds.
        /// </summary>
        public static readonly TimeSpan DefaultResponseStartTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Obsolete name for <see cref="DefaultResponseStartTimeout"/>.
        /// </summary>
        [Obsolete("Use DefaultResponseStartTimeout")]
        public static readonly TimeSpan DefaultConnectionTimeout = DefaultResponseStartTimeout;

        /// <summary>
        /// The default value for <see cref="ConfigurationBuilder.ReadTimeout(TimeSpan)"/>:
        /// 5 minutes.
        /// </summary>
        public static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The default value for <see cref="ConfigurationBuilder.BackoffResetThreshold(TimeSpan)"/>:
        /// one minute.
        /// </summary>
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
        /// The amount of time a connection must stay open before the EventSource resets its backoff delay.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.BackoffResetThreshold(TimeSpan)"/>
        public TimeSpan BackoffResetThreshold { get; }

        /// <summary>
        /// Obsolete name for <see cref="ResponseStartTimeout"/>.
        /// </summary>
        [Obsolete("Use ResponseStartTimeout")]
        public TimeSpan ConnectionTimeout => ResponseStartTimeout;

        /// <summary>
        /// The character encoding to use when reading the stream if the server did not specify
        /// an encoding in a <c>Content-Type</c> header.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.DefaultEncoding(Encoding)"/>
        public Encoding DefaultEncoding { get; }

        /// <summary>
        /// The HttpClient that will be used as the HTTP client, or null for a new HttpClient.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.HttpClient(HttpClient)"/>
        public HttpClient HttpClient { get; }

        /// <summary>
        /// The HttpMessageHandler that will be used for the HTTP client, or null for the default handler.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.HttpMessageHandler(HttpMessageHandler)"/>
        public HttpMessageHandler HttpMessageHandler { get; }

        /// <summary>
        /// Delegate hook invoked before an HTTP request has been performed.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.HttpRequestModifier(Action{HttpRequestMessage})"/>
        public Action<HttpRequestMessage> HttpRequestModifier { get; }
        
        /// <summary>
        /// The initial amount of time to wait before attempting to reconnect to the EventSource API.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.InitialRetryDelay(TimeSpan)"/>
        public TimeSpan InitialRetryDelay { get; }

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
        /// The maximum amount of time to wait before attempting to reconnect.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.MaxRetryDelay(TimeSpan)"/>
        public TimeSpan MaxRetryDelay { get; }

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
        /// The timeout when reading from the EventSource API.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.ReadTimeout(TimeSpan)"/>
        public TimeSpan ReadTimeout { get; }

        /// <summary>
        /// A factory for HTTP request body content, if the HTTP method is one that allows a request body.
        /// is one that allows a request body. This is in the form of a factory function because the request
        /// may need to be sent more than once.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.RequestBodyFactory(Func{HttpContent})"/>
        public Func<HttpContent> RequestBodyFactory { get; }

        /// <summary>
        /// The request headers to be sent with each EventSource HTTP request.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.RequestHeader(string, string)"/>
        /// <seealso cref="ConfigurationBuilder.RequestHeaders(IDictionary{string, string})"/>
        public IDictionary<string, string> RequestHeaders { get; }

        /// <summary>
        /// The maximum amount of time to wait between starting an HTTP request and receiving the response
        /// headers.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.ResponseStartTimeout(TimeSpan)"/>
        public TimeSpan ResponseStartTimeout { get; }

        /// <summary>
        /// Gets the <see cref="System.Uri"/> used when connecting to an EventSource API.
        /// </summary>
        public Uri Uri { get; }

        #endregion

        #region Internal Constructor

        internal Configuration(ConfigurationBuilder builder)
        {
            Uri = builder._uri;

            var logger = builder._logger ??
                (builder._logAdapter is null ? null : builder._logAdapter.Logger(Configuration.DefaultLoggerName));

            BackoffResetThreshold = builder._backoffResetThreshold;
            DefaultEncoding = builder._defaultEncoding ?? Encoding.UTF8;
            HttpClient = builder._httpClient;
            HttpMessageHandler = (builder._httpClient != null) ? null : builder._httpMessageHandler;
            InitialRetryDelay = builder._initialRetryDelay;
            LastEventId = builder._lastEventId;
            Logger = logger ?? Logs.None.Logger("");
            MaxRetryDelay = builder._maxRetryDelay;
            Method = builder._method;
            PreferDataAsUtf8Bytes = builder._preferDataAsUtf8Bytes;
            ReadTimeout = builder._readTimeout;
            RequestHeaders = new Dictionary<string, string>(builder._requestHeaders);
            ResponseStartTimeout = builder._responseStartTimeout;
            RequestBodyFactory = builder._requestBodyFactory;
            HttpRequestModifier = builder._httpRequestModifier; 

             
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Provides a new <see cref="ConfigurationBuilder"/> for constructing a configuration.
        /// </summary>
        /// <param name="uri">the EventSource URI</param>
        /// <returns>a new builder instance</returns>
        /// <exception cref="ArgumentNullException">if the URI is null</exception>
        public static ConfigurationBuilder Builder(Uri uri) =>
            new ConfigurationBuilder(uri);

        #endregion
    }
}