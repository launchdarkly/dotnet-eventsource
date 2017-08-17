using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
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

        private readonly HttpClient _client;
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

            _client = new HttpClient(_configuration.MessageHandler);

            _logger = _configuration.Logger ?? new LoggerFactory().CreateLogger<EventSource>();
            
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initiates the request to the EventSource API and parses Server Sent Events received by the API.
        /// </summary>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> A task that represents the work queued to execute in the ThreadPool.</returns>
        /// <exception cref="InvalidOperationException">The method was called after the connection <see cref="ReadyState"/> was Open or Connecting.</exception> 
        public async Task Start()
        {
            if (ReadyState == ReadyState.Connecting || ReadyState == ReadyState.Open)
            {
                var error = string.Format("Invalid attempt to call Start() while the connection is {0}.", ReadyState);
                _logger.LogError(error);
                throw new InvalidOperationException(error);
            }

            ConfigureRequestHeaders();

            _client.Timeout = _configuration.ConnectionTimeOut;

            ReadyState = ReadyState.Connecting;

            try
            {
                using (var stream = await _client.GetStreamAsync(_configuration.Uri))
                {
                    _eventBuffer = new List<string>();

                    ReadyState = ReadyState.Open;
                    OnOpened(new StateChangedEventArgs(ReadyState));

                    using (var reader = new System.IO.StreamReader(stream))
                    {
                        while (!reader.EndOfStream)
                        {
                            var content = reader.ReadLine();

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

                            //_logger.LogInformation("Content Received: {0}", content);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(
                    "Encountered exception in LaunchDarkly EventSource.Start method. Exception Message: {0} {1} {2}",
                    e.Message, Environment.NewLine, e.StackTrace);

                OnError(new ExceptionEventArgs(e));

                // TODO: Implement Retry

                throw;
            }
            finally
            {
                ReadyState = ReadyState.Closed;
                OnClosed(new StateChangedEventArgs(ReadyState));
            }
        }

        /// <summary>
        /// Closes the connection to the EventSource API.
        /// </summary>
        public void Close()
        {
            if (ReadyState == ReadyState.Raw || ReadyState == ReadyState.Shutdown) return;

            ReadyState = ReadyState.Shutdown;
            OnClosed(new StateChangedEventArgs(ReadyState));

            _client.CancelPendingRequests();
            _logger.LogInformation("EventSource.Close called");

        }

        #endregion

        #region Private Methods

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


        private void ConfigureRequestHeaders()
        {
            //_client.DefaultRequestHeaders.Add("Authorization", "sdk-16c73e2d-5402-4b1b-840e-cb32a4c00ce2");

            // Add all headers provided in the Configuration Headers. This allows a consumer to provide any request headers to the EventSource API
            if (_configuration.RequestHeaders != null)
            {
                foreach (var item in _configuration.RequestHeaders)
                {
                    if (!_client.DefaultRequestHeaders.Contains(item.Key))
                        _client.DefaultRequestHeaders.Add(item.Key, item.Value);
                }
            }

            // If the EventSource Configuration was provided with a LastEventId, include it as a header to the API request.
            if (!string.IsNullOrWhiteSpace(_configuration.LastEventId) && !_client.DefaultRequestHeaders.Contains(Constants.LastEventIdHttpHeader))
                _client.DefaultRequestHeaders.Add(Constants.LastEventIdHttpHeader, _configuration.LastEventId);

            // Add the Accept Header if it wasn't provided in the Configuration
            if (!_client.DefaultRequestHeaders.Contains(Constants.AcceptHttpHeader))
                _client.DefaultRequestHeaders.Add(Constants.AcceptHttpHeader, Constants.ContentType);

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
