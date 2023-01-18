using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using LaunchDarkly.EventSource.Exceptions;
using LaunchDarkly.EventSource.Internal;
using LaunchDarkly.Logging;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// Allows configuration of HTTP request behavior for <see cref="EventSource"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EventSource uses this as its default implementation of how to connect to a
    /// stream.
    /// </para>
    /// <para>
    /// If you do not need to specify any options other than the stream URI, then you
    /// do not need to reference this class directly; you can just call
    /// <see cref="Configuration.Builder(Uri)"/> to specify a URI.
    /// </para>
    /// <para>
    /// To configure additional options, obtain an instance of this class by calling
    /// <see cref="ConnectStrategy.Http(Uri)"/>, then call any of the methods of this
    /// class to specify your options. The class is immutable, so each of these methods
    /// returns a new modified instance, and an EventSource created with this
    /// configuration will not be affected by any subsequent changes you make.
    /// </para>
    /// <para>
    /// Once you have configured all desired options, pass the object to
    /// <see cref="Configuration.Builder(ConnectStrategy)"/>:
    /// </para>
    /// <example><code>
    ///     var config = Configuration.Builder(
    ///         ConnectStrategy.Http(streamUri)
    ///             .Header("name", "value")
    ///             .ReadTimeout(TimeSpan.FromMinutes(1))
    ///     );
    /// </code></example>
    /// </remarks>
    public sealed class HttpConnectStrategy : ConnectStrategy
    {
        /// <summary>
        /// The default value for <see cref="ReadTimeout(TimeSpan)"/>: 5 minutes.
        /// </summary>
        public static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The default value for <see cref="ResponseStartTimeout(TimeSpan)"/>: 10 seconds.
        /// </summary>
        public static readonly TimeSpan DefaultResponseStartTimeout = TimeSpan.FromSeconds(10);

        private readonly Uri _uri;
        private readonly ChainableTransform<HttpClient> _clientTransform;
        private readonly ChainableTransform<HttpRequestMessage> _requestTransform;
        private readonly HttpClient _httpClient;
        private readonly HttpMessageHandler _httpMessageHandler;
        private readonly TimeSpan? _readTimeout;

        /// <inheritdoc/>
        public override Uri Origin => _uri;

        private sealed class ChainableTransform<T>
        {
            private readonly Action<T> _action;

            public ChainableTransform() : this(null) { }

            private ChainableTransform(Action<T> action) { _action = action; }

            public void Apply(T target) =>
                _action?.Invoke(target);

            public ChainableTransform<T> AndThen(Action<T> nextAction)
            {
                if (nextAction is null)
                {
                    return this;
                }
                return new ChainableTransform<T>(
                    _action is null ? nextAction :
                        target =>
                        {
                            _action(target);
                            nextAction(target);
                        });
            }
        }

        internal HttpConnectStrategy(Uri uri) :
            this(uri, null, null, null, null, null) { }

        private HttpConnectStrategy(
            Uri uri,
            ChainableTransform<HttpClient> clientTransform,
            ChainableTransform<HttpRequestMessage> requestTransform,
            HttpClient httpClient,
            HttpMessageHandler httpMessageHandler,
            TimeSpan? readTimeout
            )
        {
            if (uri is null)
            {
                throw new ArgumentNullException("uri");
            }
            _uri = uri;
            _clientTransform = clientTransform ?? new ChainableTransform<HttpClient>();
            _requestTransform = requestTransform ?? new ChainableTransform<HttpRequestMessage>();
            _httpClient = httpClient;
            _httpMessageHandler = httpMessageHandler;
            _readTimeout = readTimeout;
        }

        /// <summary>
        /// Called by EventSource to set up the client.
        /// </summary>
        /// <param name="logger">the configured logger</param>
        /// <returns>the client implementation</returns>
        public override Client CreateClient(Logger logger) =>
            new ClientImpl(this, logger);

        /// <summary>
        /// Sets a custom header to be sent in each request.
        /// </summary>
        /// <remarks>
        /// Any existing headers with the same name are overwritten.
        /// </remarks>
        /// <param name="name">the header name</param>
        /// <param name="value">the header value</param>
        /// <returns>a new HttpConnectStrategy instance with this property modified</returns>
        public HttpConnectStrategy Header(string name, string value) =>
            AddRequestTransform(r =>
                {
                    r.Headers.Remove(name);
                    r.Headers.Add(name, value);
                });

        /// <summary>
        /// Sets request headers to be sent in each request.
        /// </summary>
        /// <remarks>
        /// Any existing headers with the same names are overwritten.
        /// </remarks>
        /// <param name="headers">the headers (null is equivalent to an empty dictionary)</param>
        /// <returns>a new HttpConnectStrategy instance with this property modified</returns>
        public HttpConnectStrategy Headers(IDictionary<string, string> headers) =>
            headers is null || headers.Count == 0 ? this :
                AddRequestTransform(r =>
                {
                    foreach (var item in headers)
                    {
                        r.Headers.Remove(item.Key);
                        r.Headers.Add(item.Key, item.Value);
                    }
                });

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
        /// or <see cref="ResponseStartTimeout"/>, those methods will be ignored.
        /// </para>
        /// </remarks>
        /// <param name="client">an HttpClient instance, or null to use the default behavior</param>
        /// <returns>a new HttpConnectStrategy instance with this property modified</returns>
        public HttpConnectStrategy HttpClient(HttpClient client) =>
            new HttpConnectStrategy(
                _uri,
                _clientTransform,
                _requestTransform,
                client,
                _httpMessageHandler,
                _readTimeout
                );

        /// <summary>
        /// Sets a delegate hook invoked after an HTTP client is created.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Use this if you need to set client properties other than the ones that are
        /// already supported by <see cref="HttpConnectStrategy"/> methods.
        /// </para>
        /// <para>
        /// If you call this method multiple times, the transformations are applied in
        /// the same order as the calls.
        /// </para>
        /// <para>
        /// This method is ignored if you specified your own client instance with
        /// <see cref="HttpClient(System.Net.Http.HttpClient)"/>.
        /// </para>
        /// </remarks>
        /// <param name="httpClientModifier">code that will be called to modify the client</param>
        /// <returns>a new HttpConnectStrategy instance with this property modified</returns>
        public HttpConnectStrategy HttpClientModifier(Action<HttpClient> httpClientModifier) =>
            AddClientTransform(httpClientModifier);

        /// <summary>
        /// Sets the HttpMessageHandler that will be used for the HTTP client.
        /// </summary>
        /// <remarks>
        /// If you have specified a custom HTTP client instance with <see cref="HttpClient"/>, then
        /// <see cref="HttpMessageHandler(HttpMessageHandler)"/> is ignored.
        /// </remarks>
        /// <param name="handler">the message handler implementation, or null for the
        /// default handler</param>
        /// <returns>a new HttpConnectStrategy instance with this property modified</returns>
        public HttpConnectStrategy HttpMessageHandler(HttpMessageHandler handler) =>
            new HttpConnectStrategy(
                _uri,
                _clientTransform,
                _requestTransform,
                _httpClient,
                handler,
                _readTimeout
                );

        /// <summary>
        /// Sets a delegate hook invoked before an HTTP request is performed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Use this if you need to set request properties other than the ones that are
        /// already supported by <see cref="HttpConnectStrategy"/> methods, or if you
        /// need to determine the request properties dynamically rather than setting them
        /// to fixed values initially.
        /// </para>
        /// <para>
        /// If you call this method multiple times, the transformations are applied in
        /// the same order as the calls.
        /// </para>
        /// </remarks>
        /// <param name="httpRequestModifier">code that will be called with the request
        /// before it is sent</param>
        /// <returns>a new HttpConnectStrategy instance with this property modified</returns>
        public HttpConnectStrategy HttpRequestModifier(Action<HttpRequestMessage> httpRequestModifier) =>
            AddRequestTransform(httpRequestModifier);

        /// <summary>
        /// Sets the HTTP method that will be used when connecting to the EventSource API.
        /// </summary>
        /// <remarks>
        /// By default, this is <see cref="HttpMethod.Get"/>.
        /// </remarks>
        /// <param name="method">the method; defaults to Get if null</param>
        /// <returns>a new HttpConnectStrategy instance with this property modified</returns>
        public HttpConnectStrategy Method(HttpMethod method) =>
            AddRequestTransform(r => r.Method = method ?? HttpMethod.Get);

        /// <summary>
        /// Sets a timeout that will cause the stream to be closed if the timeout is
        /// exceeded when reading data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The connection will be automatically dropped and restarted if the server sends
        /// no data within this interval. This prevents keeping a stale connection that may
        /// no longer be working. It is common for SSE servers to send a simple comment line
        /// (":") as a heartbeat to prevent timeouts.
        /// </para>
        /// <para>
        /// The default value is <see cref="DefaultReadTimeout"/>.
        /// </para>
        /// </remarks>
        /// <param name="readTimeout">the timeout</param>
        /// <returns>a new HttpConnectStrategy instance with this property modified</returns>
        public HttpConnectStrategy ReadTimeout(TimeSpan readTimeout) =>
            new HttpConnectStrategy(
                _uri,
                _clientTransform,
                _requestTransform,
                _httpClient,
                _httpMessageHandler,
                ConfigurationBuilder.TimeSpanCanBeInfinite(readTimeout)
                );

        /// <summary>
        /// Sets a factory for HTTP request body content, if the HTTP method is one that allows a request body.
        /// </summary>
        /// <remarks>
        /// This is in the form of a factory function because the request may need to be sent more than once.
        /// </remarks>
        /// <param name="factory">the factory function</param>
        /// <returns>a new HttpConnectStrategy instance with this property modified</returns>
        public HttpConnectStrategy RequestBodyFactory(Func<HttpContent> factory) =>
            AddRequestTransform(r => r.Content = factory());

        /// <summary>
        /// Sets the maximum amount of time EventSource will wait between starting an HTTP request and
        /// receiving the response headers.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the same as the <c>Timeout</c> property in .NET's <c>HttpClient</c>. The default value is
        /// <see cref="DefaultResponseStartTimeout"/>.
        /// </para>
        /// <para>
        /// It is <i>not</i> the same as a TCP connection timeout. A connection timeout would include only the
        /// time of establishing the connection, not the time it takes for the server to prepare the beginning
        /// of the response. .NET does not consistently support a connection timeout, but if you are using .NET
        /// Core or .NET 5+ you can implement it by using <c>SocketsHttpHandler</c> as your
        /// <see cref="HttpMessageHandler(System.Net.Http.HttpMessageHandler)"/> and setting the
        /// <c>ConnectTimeout</c> property there.
        /// </para>
        /// </remarks>
        /// <param name="timeout">the timeout</param>
        /// <returns>a new HttpConnectStrategy instance with this property modified</returns>
        public HttpConnectStrategy ResponseStartTimeout(TimeSpan timeout) =>
            AddClientTransform(c => c.Timeout = ConfigurationBuilder.TimeSpanCanBeInfinite(timeout));

        /// <summary>
        /// Specifies a different stream URI.
        /// </summary>
        /// <param name="uri">the stream URI; must not be null</param>
        /// <returns>a new HttpConnectStrategy instance with this property modified</returns>
        public HttpConnectStrategy Uri(Uri uri) =>
            new HttpConnectStrategy(
                uri,
                _clientTransform,
                _requestTransform,
                _httpClient,
                _httpMessageHandler,
                _readTimeout
                );

        private HttpConnectStrategy AddClientTransform(Action<HttpClient> addedAction) =>
            addedAction is null ? this :
                new HttpConnectStrategy(_uri, _clientTransform.AndThen(addedAction), _requestTransform,
                    _httpClient, _httpMessageHandler, _readTimeout);

        // This method is used to chain together all actions that affect the HTTP request
        private HttpConnectStrategy AddRequestTransform(Action<HttpRequestMessage> addedAction) =>
            addedAction is null ? this :
                new HttpConnectStrategy(_uri, _clientTransform, _requestTransform.AndThen(addedAction),
                    _httpClient, _httpMessageHandler, _readTimeout);

        internal sealed class ClientImpl : Client
        {
            private readonly HttpConnectStrategy _config;
            private readonly HttpClient _httpClient;
            private readonly bool _disposeClient;
            private readonly Logger _logger;

            // visible for testing
            internal HttpClient HttpClient => _httpClient;

            public ClientImpl(HttpConnectStrategy config, Logger logger)
            {
                _config = config;
                _logger = logger;

                if (_config._httpClient is null)
                {
                    _httpClient = _config._httpMessageHandler is null ?
                        new HttpClient() :
                        new HttpClient(_config._httpMessageHandler, false);
                    _config._clientTransform.Apply(_httpClient);
                    _disposeClient = true;
                }
                else
                {
                    _httpClient = _config._httpClient;
                    _disposeClient = false;
                }
            }

            public override void Dispose()
            {
                if (_disposeClient)
                {
                    _httpClient.Dispose();
                }
            }

            public override async Task<Result> ConnectAsync(Params p)
            {
                var request = CreateRequest(p);
                _logger.Debug("Making {0} request to EventSource URI {1}",
                    request.Method, _config._uri);
                HttpResponseMessage response;
                response = await _httpClient.SendAsync(request,
                    HttpCompletionOption.ResponseHeadersRead,
                    p.CancellationToken).ConfigureAwait(false);
                var valid = false;
                try
                {
                    ValidateResponse(response);
                    valid = true;
                }
                finally
                {
                    if (!valid)
                    {
                        response.Dispose();
                    }
                }
                return new Result
                {
                    Stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false),
                    ReadTimeout = _config._readTimeout,
                    Closer = response
                };
            }

            private HttpRequestMessage CreateRequest(Params p)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, _config._uri);

                if (!string.IsNullOrWhiteSpace(p.LastEventId))
                {
                    request.Headers.Remove(Constants.LastEventIdHttpHeader);
                    request.Headers.Add(Constants.LastEventIdHttpHeader, p.LastEventId);
                }

                _config._requestTransform.Apply(request);

                // The Accept header must always be sent by SSE clients
                if (!request.Headers.Contains(Constants.AcceptHttpHeader))
                {
                    request.Headers.Add(Constants.AcceptHttpHeader, Constants.EventStreamContentType);
                }

                request.Headers.ExpectContinue = false;
                request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };

                return request;
            }

            private void ValidateResponse(HttpResponseMessage response)
            {
                // Any non-2xx response status is an error. A 204 (no content) is also an error.
                if (!response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    throw new StreamHttpErrorException((int)response.StatusCode);
                }

                if (response.Content is null)
                {
                    throw new StreamClosedByServerException();
                }

                var contentType = response.Content.Headers.ContentType;
                var encoding = DetectEncoding(response);
                if (contentType.MediaType != Constants.EventStreamContentType || encoding != Encoding.UTF8)
                {
                    throw new StreamContentException(contentType, encoding);
                }
            }

            private static Encoding DetectEncoding(HttpResponseMessage response)
            {
                var charset = response.Content.Headers.ContentType?.CharSet;
                if (charset != null)
                {
                    try
                    {
                        return Encoding.GetEncoding(charset);
                    }
                    catch (ArgumentException) { }
                }
                return Encoding.UTF8;
            }
        }
    }
}
