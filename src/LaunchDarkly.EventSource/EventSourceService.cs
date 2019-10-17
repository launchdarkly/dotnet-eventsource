using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;

namespace LaunchDarkly.EventSource
{
    internal class EventSourceService
    {
        #region Private Fields

        private readonly Configuration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILog _logger;

        private const string UserAgentProduct = "DotNetClient";
        internal static readonly string UserAgentVersion = ((AssemblyInformationalVersionAttribute)typeof(EventSource)
                .GetTypeInfo()
                .Assembly
                .GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute)))
            .InformationalVersion;

        #endregion

        #region Public Events

        /// <summary>
        /// Occurs when the connection to the EventSource API has been opened.
        /// </summary>
        public event EventHandler<EventArgs> ConnectionOpened;
        /// <summary>
        /// Occurs when the connection to the EventSource API has been closed.
        /// </summary>
        public event EventHandler<EventArgs> ConnectionClosed;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EventSourceService" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <exception cref="ArgumentNullException">client
        /// or
        /// configuration</exception>
        public EventSourceService(Configuration configuration, HttpClient httpClient, ILog logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initiates the request to the EventSource API and parses Server Sent Events received by the API.
        /// </summary>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> A task that represents the work queued to execute in the ThreadPool.</returns>
        public async Task GetDataAsync(Action<string> processResponse, string lastEventId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ConnectToEventSourceApi(processResponse, lastEventId, cancellationToken);
        }

        #endregion

        #region Private Methods

        private async Task ConnectToEventSourceApi(Action<string> processResponse, string lastEventId, CancellationToken cancellationToken)
        {
            _logger.DebugFormat("Making {0} request to EventSource URI {1}",
                _configuration.Method ?? HttpMethod.Get,
                _configuration.Uri);

            using (var response = await _httpClient.SendAsync(CreateHttpRequestMessage(_configuration.Uri, lastEventId),
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false))
            {
                _logger.DebugFormat("Response status: {0}", (int)response.StatusCode);
                HandleInvalidResponses(response);

                OnConnectionOpened();

                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    using (var reader = GetStreamReader(stream))
                    {
                        await ProcessResponseFromReaderAsync(processResponse, reader, cancellationToken);
                    }
                }

                OnConnectionClosed();
            }
        }

        protected virtual IStreamReader GetStreamReader(Stream stream)
        {
            return new EventSourceStreamReader(stream);
        }

        protected virtual async Task ProcessResponseFromReaderAsync(Action<string> processResponse, IStreamReader reader,
            CancellationToken cancellationToken)
        {
            // In a loop, reading from a stream. while reading from the stream, process the lines in the stream.
            // Reset the timer after processing each line (reading from the stream).
            // If the read time out occurs, throw exception and exit method.

            while (!cancellationToken.IsCancellationRequested)
            {
                var readTimeoutTask = Task.Delay(_configuration.ReadTimeout);

                var readLineTask = reader.ReadLineAsync();

                var completedTask = await Task.WhenAny(readLineTask, readTimeoutTask);

                if (completedTask == readTimeoutTask)
                {
                    Util.SuppressExceptions(readLineTask); // must do this since we're not going to await the task
                    throw new HttpRequestException(Resources.EventSourceService_Read_Timeout);
                }
                else
                {
                    Util.SuppressExceptions(readTimeoutTask); // this task should never throw an exception, but you never know
                }

                string line = readLineTask.Result;
                if (line == null)
                {
                    // this means the stream is done, i.e. the connection was closed
                    return;
                }
                processResponse(line);
            }
        }

        private HttpRequestMessage CreateHttpRequestMessage(Uri uri, string lastEventId)
        {
            var request = new HttpRequestMessage(_configuration.Method ?? HttpMethod.Get, uri);

            // Add all headers provided in the Configuration Headers. This allows a consumer to provide any request headers to the EventSource API
            if (_configuration.RequestHeaders != null)
            {
                foreach (var item in _configuration.RequestHeaders)
                {
                    request.Headers.Add(item.Key, item.Value);
                }
            }

            // Add the request body, if any.
            if (_configuration.RequestBodyFactory != null)
            {
                HttpContent requestBody = _configuration.RequestBodyFactory();
                if (requestBody != null)
                {
                    request.Content = requestBody;
                }
            }

            // If the lastEventId was provided, include it as a header to the API request.
            if (!string.IsNullOrWhiteSpace(lastEventId))
            {
                request.Headers.Remove(Constants.LastEventIdHttpHeader);
                request.Headers.Add(Constants.LastEventIdHttpHeader, lastEventId);
            }

            // If we haven't set the LastEventId header and if the EventSource Configuration was provided with a LastEventId,
            // include it as a header to the API request.
            if (!string.IsNullOrWhiteSpace(_configuration.LastEventId) && !request.Headers.Contains(Constants.LastEventIdHttpHeader))
                request.Headers.Add(Constants.LastEventIdHttpHeader, _configuration.LastEventId);

            if (request.Headers.UserAgent.Count == 0)
                request.Headers.UserAgent.ParseAdd(UserAgentProduct + "/" + UserAgentVersion);

            // Add the Accept Header if it wasn't provided in the Configuration
            if (!request.Headers.Contains(Constants.AcceptHttpHeader))
                request.Headers.Add(Constants.AcceptHttpHeader, Constants.EventStreamContentType);

            request.Headers.ExpectContinue = false;
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };

            return request;
        }

        private void HandleInvalidResponses(HttpResponseMessage response)
        {
            HandleUnsuccessfulStatusCodes(response);

            // According to Specs, a client can be told to stop reconnecting using the HTTP 204 No Content response code
            HandleNoContent(response);

            // According to Specs, HTTP 200 OK responses that have a Content-Type specifying an unsupported type, 
            // or that have no Content-Type at all, must cause the user agent to fail the connection.
            HandleIncorrectMediaType(response);
        }

        private void HandleNoContent(HttpResponseMessage response)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                throw new EventSourceServiceUnsuccessfulResponseException(Resources.EventSource_204_Response, (int)response.StatusCode);
            }

            if (response.Content == null)
            {
                throw new EventSourceServiceCancelledException(Resources.EventSource_Response_Content_Empty);
            }
        }

        private void HandleIncorrectMediaType(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode && response.Content != null && response.Content.Headers.ContentType.MediaType !=
                Constants.EventStreamContentType)
            {
                throw new EventSourceServiceCancelledException(Resources.EventSource_Invalid_MediaType);
            }
        }

        private void HandleUnsuccessfulStatusCodes(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode == false)
            {
                throw new EventSourceServiceUnsuccessfulResponseException(string.Format(Resources.EventSource_HttpResponse_Not_Successful, (int)response.StatusCode),
                    (int)response.StatusCode);
            }
        }

        private void OnConnectionOpened()
        {
            ConnectionOpened?.Invoke(this, EventArgs.Empty);
        }

        private void OnConnectionClosed()
        {
            ConnectionClosed?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}
