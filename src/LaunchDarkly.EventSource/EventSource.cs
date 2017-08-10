using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace LaunchDarkly.EventSource
{
    public sealed class EventSource
    {
        private readonly HttpClient _client;
        private readonly Configuration _configuration;
        private readonly ILogger _logger;

        //private TimeSpan _connectionTimeout = Timeout.InfiniteTimeSpan;
      

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

            if (_configuration.LoggerFactory != null)
                _logger = _configuration.LoggerFactory.CreateLogger<EventSource>();

        }
        
        public async Task Start()
        {
            if (ReadyState == ReadyState.Connecting || ReadyState == ReadyState.Open)
            {
                throw new InvalidOperationException("Cannot call connect while connection is " + ReadyState);
            }

            if (_configuration.HttpRequestHeaders != null)
            {
                foreach (var item in _configuration.HttpRequestHeaders)
                {
                    _client.DefaultRequestHeaders.Add(item.Key, item.Value);
                }
            }

            //_client.DefaultRequestHeaders.Add("Authorization", "sdk-16c73e2d-5402-4b1b-840e-cb32a4c00ce2");
            _client.Timeout = _configuration.ConnectionTimeOut;

            ReadyState = ReadyState.Connecting;

            try
            {
                using (var stream = await _client.GetStreamAsync(_configuration.Uri))
                {
                    ReadyState = ReadyState.Open;
                    OnOpened(new StateChangedEventArgs(ReadyState));

                    using (var reader = new StreamReader(stream))
                    {
                        while (true)
                        {
                            string content = reader.ReadLine();

                            //TODO: Parsing 
                            //OnMessageReceived(new MessageReceivedEventArgs(null, ""));

                            _logger.LogInformation("Content Received: {0}", content);
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

                throw;
            }
            finally
            {
                ReadyState = ReadyState.Closed;
                OnClosed(new StateChangedEventArgs(ReadyState));
            }
        }

        public void Close()
        {
            if (ReadyState == ReadyState.Raw || ReadyState == ReadyState.Shutdown) return;

            ReadyState = ReadyState.Shutdown;
            OnClosed(new StateChangedEventArgs(ReadyState));

            _client.CancelPendingRequests();
            _logger.LogInformation("EventSource.Close called");

        }

        private void OnOpened(StateChangedEventArgs e)
        {
            Opened?.Invoke(this, e);
        }

        private void OnClosed(StateChangedEventArgs e)
        {
            Closed?.Invoke(this, e);
        }

        private void OnMessageReceived(MessageReceivedEventArgs e)
        {
            MessageReceived?.Invoke(this, e);
        }

        private void OnCommentReceived(CommentReceivedEventArgs e)
        {
            CommentReceived?.Invoke(this, e);
        }

        private void OnError(ExceptionEventArgs e)
        {
            Error?.Invoke(this, e);
        }

    }
}
