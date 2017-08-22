using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

// Added to allow the Test Project to access internal types and methods.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("LaunchDarkly.EventSource.Tests")]

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// Provides an EventSource client for consuming Server Sent Events. Additional details on the Server Sent Events spec 
    /// can be found at https://html.spec.whatwg.org/multipage/server-sent-events.html
    /// </summary>
    public sealed class EventSource
    {

        #region Private Fields

        private readonly Configuration _configuration;
        private readonly ILogger _logger;

        //private TimeSpan _connectionTimeout = Timeout.InfiniteTimeSpan;
        private List<string> _eventBuffer;
        private string _eventName = Constants.MessageField;
        private string _lastEventId;
        private TimeSpan _retryDelay = TimeSpan.FromSeconds(1);

        internal static readonly string Version = ((AssemblyInformationalVersionAttribute)typeof(EventSource)
                .GetTypeInfo()
                .Assembly
                .GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute)))
            .InformationalVersion;

        #endregion

        #region Public Events

        /// <summary>
        /// Occurs when the connection to the EventSource API has been opened.
        /// </summary>
        public event EventHandler<StateChangedEventArgs> Opened;
        /// <summary>
        /// Occurs when the connection to the EventSource API has been closed.
        /// </summary>
        public event EventHandler<StateChangedEventArgs> Closed;
        /// <summary>
        /// Occurs when a Server Sent Event from the EventSource API has been received.
        /// </summary>
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        /// <summary>
        /// Occurs when a comment has been received from the EventSource API.
        /// </summary>
        public event EventHandler<CommentReceivedEventArgs> CommentReceived;
        /// <summary>
        /// Occurs when an error has happened when the EventSource is open and processing Server Sent Events.
        /// </summary>
        public event EventHandler<ExceptionEventArgs> Error;

        #endregion Public Events

        #region Public Properties

        /// <summary>
        /// Gets the state of the EventSource connection.
        /// </summary>
        /// <value>
        /// One of the <see cref="EventSource.ReadyState"/> values, which represents the state of the EventSource connection.
        /// </value>
        public ReadyState ReadyState
        {
            get;
            private set;
        }

        #endregion

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EventSource" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <exception cref="ArgumentNullException">client
        /// or
        /// configuration</exception>
        public EventSource(Configuration configuration)
        {
            //System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls12;

            ReadyState = ReadyState.Raw;

            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            _logger = _configuration.Logger ?? new LoggerFactory().CreateLogger<EventSource>();
            
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initiates the request to the EventSource API and parses Server Sent Events received by the API.
        /// </summary>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> A task that represents the work queued to execute in the ThreadPool.</returns>
        /// <exception cref="InvalidOperationException">The method was called after the connection <see cref="ReadyState"/> was Open or Connecting.</exception> 
        public async Task StartAsync()
        {
            if (ReadyState == ReadyState.Connecting || ReadyState == ReadyState.Open)
            {
                var error = string.Format("Invalid attempt to call Start() while the connection is {0}.", ReadyState);
                _logger.LogError(error);
                throw new InvalidOperationException(error);
            }

            SetReadyState(ReadyState.Connecting);

            var requestMessage = CreateHttpRequestMessage(_configuration.Uri);

            var client = GetHttpClient();

            try
            {
                using (var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead,
                    CancellationToken.None).ConfigureAwait(false))
                {
                    // According to Specs, a client can be told to stop reconnecting using the HTTP 204 No Content response code
                    if (HandleNoContent(response)) return;

                    // According to Specs, HTTP 200 OK responses that have a Content-Type specifying an unsupported type, 
                    // or that have no Content-Type at all, must cause the user agent to fail the connection.
                    if (HandleIncorrectMediaType(response)) return;

                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        _eventBuffer = new List<string>();

                        SetReadyState(ReadyState.Open, OnOpened);

                        using (var reader = new StreamReader(stream))
                        {
                            //while (!cancellationToken.IsCancellationRequested && !reader.EndOfStream)
                            while (ReadyState == ReadyState.Open && !reader.EndOfStream)
                            {
                                var content = await reader.ReadLineAsync().ConfigureAwait(false);

                                ProcessResponseContent(content);
                            }

                        }
                    }
                }

            }
            catch (Exception e)
            {
                _logger.LogError(
                    "Encountered exception in LaunchDarkly EventSource.Start method. Exception Message: {0} {1} {2}",
                    e.Message, Environment.NewLine, e.StackTrace);

                CloseAndRaiseError(e);

                // TODO: Implement Retry
                //throw;
            }
            finally
            {
                if (client != null)
                {
                    client.Dispose();
                }
            }

            SetReadyState(ReadyState.Closed, OnClosed);
        }

        /// <summary>
        /// Closes the connection to the EventSource API.
        /// </summary>
        public void Close()
        {
            if (ReadyState == ReadyState.Raw || ReadyState == ReadyState.Shutdown) return;

            Close(ReadyState.Shutdown);
        }
        
        #endregion

        #region Private Methods

        private HttpClient GetHttpClient()
        {
            return new HttpClient(_configuration.MessageHandler, false) { Timeout = _configuration.ConnectionTimeOut };
        }

        private void Close(ReadyState state)
        {
            ReadyState = state;
            OnClosed(new StateChangedEventArgs(ReadyState));

            //_client.CancelPendingRequests();
            _logger.LogInformation("EventSource.Close called");
        }

        private void CloseAndRaiseError(Exception ex)
        {
            if (ex == null)
                throw new ArgumentNullException(nameof(ex));

            Close(ReadyState.Closed);

            OnError(new ExceptionEventArgs(ex));
        }
        
        private bool HandleNoContent(HttpResponseMessage response)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                CloseAndRaiseError(new OperationCanceledException(
                    "Remote EventSource API returned Http Status Code 204."));
                return true;
            }
            return false;
        }

        private bool HandleIncorrectMediaType(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode && response.Content.Headers.ContentType.MediaType !=
                Constants.EventStreamContentType)
            {
                CloseAndRaiseError(new OperationCanceledException(
                    "HTTP Content-Type returned from the remote EventSource API does not match 'text/event-stream'."));
                return true;
            }
            return false;
        }

        private void ProcessResponseContent(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                DispatchEvent();
            }
            else if (EventParser.IsComment(content))
            {
                OnCommentReceived(new CommentReceivedEventArgs(content));
            }
            else if (EventParser.ContainsField(content))
            {
                var field = EventParser.GetFieldFromLine(content);

                ProcessField(field.Key, field.Value);
            }
            else
            {
                ProcessField(content.Trim(), string.Empty);
            }
        }

        private void SetReadyState(ReadyState state, Action<StateChangedEventArgs> action = null)
        {
            if (ReadyState == state) return;

            ReadyState = state;

            if (action != null)
                action(new StateChangedEventArgs(ReadyState));
        }
        
        private void ProcessField(string field, string value)
        {
            if (EventParser.IsDataFieldName(field))
            {
                _eventBuffer.Add(value);
                _eventBuffer.Add("\n");
            }
            else if (EventParser.IsIdFieldName(field))
            {
                _lastEventId = value;
            }
            else if (EventParser.IsEventFieldName(field))
            {
                _eventName = value;
            }
            else if (EventParser.IsRetryFieldName(field) && EventParser.IsStringNumeric(value))
            {
                long retry;

                if (long.TryParse(value, out retry))
                    _retryDelay = TimeSpan.FromMilliseconds(retry);
            }
        }

        private void DispatchEvent()
        {
            if (_eventBuffer.Count == 0) return;

            _eventBuffer.RemoveAll(item => item.Equals("\n"));

            var message = new MessageEvent(string.Concat(_eventBuffer), _lastEventId, _configuration.Uri);

            OnMessageReceived(new MessageReceivedEventArgs(message, _eventName));
            
            _eventBuffer.Clear();
            _eventName = Constants.MessageField;
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

            // Add the Accept Header if it wasn't provided in the Configuration
            if (!request.Headers.Contains(Constants.AcceptHttpHeader))
                request.Headers.Add(Constants.AcceptHttpHeader, Constants.EventStreamContentType);

            request.Headers.ExpectContinue = false;
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };

            return request;
        }

        private void OnOpened(StateChangedEventArgs e)
        {
            if (Opened != null)
            {
                Opened(this, e);
            }
        }

        private void OnClosed(StateChangedEventArgs e)
        {
            if (Closed != null)
            {
                Closed(this, e);
            }
        }

        private void OnMessageReceived(MessageReceivedEventArgs e)
        {
            if (MessageReceived != null)
            {
                MessageReceived(this, e);
            }
        }

        private void OnCommentReceived(CommentReceivedEventArgs e)
        {
            if (CommentReceived != null)
            {
                CommentReceived(this, e);
            }
        }

        private void OnError(ExceptionEventArgs e)
        {
            if (Error != null)
            {
                Error(this, e);
            }
        }

        #endregion

    }
}
