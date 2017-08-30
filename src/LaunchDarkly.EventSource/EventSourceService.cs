using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace LaunchDarkly.EventSource
{
    internal class EventSourceService
    {
        #region Private Fields

        private readonly Configuration _configuration;
        private readonly ILogger _logger;

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
        public EventSourceService(Configuration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _logger = _configuration.Logger ?? new LoggerFactory().CreateLogger<EventSource>();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initiates the request to the EventSource API and parses Server Sent Events received by the API.
        /// </summary>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> A task that represents the work queued to execute in the ThreadPool.</returns>
        public async Task GetDataAsync(Action<string> processResponse, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ConnectToEventSourceApi(processResponse, cancellationToken);

        }

        #endregion

        #region Private Methods

        private async Task ConnectToEventSourceApi(Action<string> processResponse, CancellationToken cancellationToken)
        {
            var client = GetHttpClient();

            try
            {
                using (var response = await client.SendAsync(CreateHttpRequestMessage(_configuration.Uri),
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false))
                {
                    HandleInvalidResponses(response);
                    
                    OnConnectionOpened();

                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            while (!cancellationToken.IsCancellationRequested && !reader.EndOfStream)
                            {
                                var readTimeoutTask = Task.Delay(_configuration.ReadTimeOut);

                                var readLineTask = reader.ReadLineAsync();

                                var completedTask = await Task.WhenAny(readLineTask, readTimeoutTask);

                                if (completedTask == readTimeoutTask)
                                {
                                    // Reading Timed out.
                                    throw new HttpRequestException(Resources.EventSourceService_Read_Timeout);
                                }

                                processResponse(readLineTask.Result);
                            }
                        }
                    }

                    OnConnectionClosed();
                }
            }
            catch (Exception e)
            {
                _logger.LogError(Resources.EventSource_Logger_Connection_Error,
                    e.Message, Environment.NewLine, e.StackTrace);

                throw;
            }
            finally
            {
                if (client != null)
                {
                    client.Dispose();
                }
            }

        }

        private HttpClient GetHttpClient()
        {
            return new HttpClient(_configuration.MessageHandler, false) { Timeout = _configuration.ConnectionTimeOut };
        }

        private HttpRequestMessage CreateHttpRequestMessage(Uri uri)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, uri);

            // Add all headers provided in the Configuration Headers. This allows a consumer to provide any request headers to the EventSource API
            if (_configuration.RequestHeaders != null)
            {
                foreach (var item in _configuration.RequestHeaders)
                {
                    request.Headers.Add(item.Key, item.Value);
                }
            }

            // If the EventSource Configuration was provided with a LastEventId, include it as a header to the API request.
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

                throw new EventSourceServiceCancelledException(Resources.EventSource_204_Response);
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
                throw new EventSourceServiceCancelledException(string.Format(Resources.EventSource_HttpResponse_Not_Successful, (int)response.StatusCode));
            }
        }

        private void OnConnectionOpened()
        {
            if (ConnectionOpened != null)
            {
                ConnectionOpened(this, EventArgs.Empty);
            }
        }

        private void OnConnectionClosed()
        {
            if (ConnectionClosed != null)
            {
                ConnectionClosed(this, EventArgs.Empty);
            }
        }

        #endregion
    }
}
