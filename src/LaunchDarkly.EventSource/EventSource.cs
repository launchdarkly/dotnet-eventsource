using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LaunchDarkly.EventSource
{
    public sealed class EventSource
    {
        private readonly HttpClient _client;
        private readonly Configuration _configuration;
        private readonly ILogger _logger;

        //private TimeSpan _connectionTimeout = Timeout.InfiniteTimeSpan;
        private List<string> _eventBuffer;
        private string _eventName = Constants.MessageField;

        public event EventHandler<StateChangedEventArgs> Opened;
        public event EventHandler<StateChangedEventArgs> Closed;
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        public event EventHandler<CommentReceivedEventArgs> CommentReceived;
        public event EventHandler<ExceptionEventArgs> Error;

        public ReadyState ReadyState
        {
            get;
            private set;
        }

        public EventSource(Configuration configuration) : this (new HttpClient(), configuration)
        {
            //System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls12;
        }

        public EventSource(HttpClient client, Configuration configuration)
        {
            ReadyState = ReadyState.Raw;

            _client = client ?? throw new ArgumentNullException(nameof(client));

            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            var loggerFactory = _configuration.LoggerFactory ?? new LoggerFactory();

            _logger = loggerFactory.CreateLogger<EventSource>();

        }
        
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
                        while (true) //(!reader.EndOfStream)
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

        private void ProcessField(string field, string value)
        {
            if (EventParser.IsDataField(field))
            {
                _eventBuffer.Add(value);
                _eventBuffer.Add("\n");
            }
            else if (EventParser.IsIdField(field))
            {
                _configuration.LastEventId = value;
            }
            else if (EventParser.IsEventField(field))
            {
                _eventName = value;
            }
            else if (EventParser.IsRetryField(field) && EventParser.IsStringNumeric(value))
            {
                long retry;

                if (long.TryParse(value, out retry))
                    _configuration.DelayRetryDuration = TimeSpan.FromMilliseconds(retry);
            }
        }

        private void DispatchEvent()
        {
            if (_eventBuffer.Count == 0) return;

            var message = new MessageEvent(string.Concat(_eventBuffer), _configuration.LastEventId, _configuration.Uri);

            OnMessageReceived(new MessageReceivedEventArgs(message, _eventName));
            
            _eventBuffer.Clear();
            _eventName = Constants.MessageField;
        }

        public void Close()
        {
            if (ReadyState == ReadyState.Raw || ReadyState == ReadyState.Shutdown) return;

            ReadyState = ReadyState.Shutdown;
            OnClosed(new StateChangedEventArgs(ReadyState));

            _client.CancelPendingRequests();
            _logger.LogInformation("EventSource.Close called");

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

    }
}
