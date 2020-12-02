using Common.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// Provides an EventSource client for consuming Server Sent Events. Additional details on the Server Sent Events spec
    /// can be found at https://html.spec.whatwg.org/multipage/server-sent-events.html
    /// </summary>
    public class EventSource : IEventSource, IDisposable
    {
        #region Private Fields

        private readonly Configuration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILog _logger;

        private List<string> _eventBuffer;
        private string _eventName = Constants.MessageField;
        private string _lastEventId;
        private TimeSpan _retryDelay;
        private readonly ExponentialBackoffWithDecorrelation _backOff;
        private CancellationTokenSource _currentRequestToken;
        private DateTime? _lastSuccessfulConnectionTime;
        private ReadyState _readyState;

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
            get
            {
                lock(this)
                {
                    return _readyState;
                }
            }
            private set
            {
                lock(this)
                {
                    _readyState = value;
                }
            }
        }

        internal TimeSpan BackOffDelay
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
            _readyState = ReadyState.Raw;

            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _logger = _configuration.Logger ?? LogManager.GetLogger(typeof(EventSource));

            _retryDelay = _configuration.DelayRetryDuration;

            _backOff = new ExponentialBackoffWithDecorrelation(_retryDelay,
                Configuration.MaximumRetryDuration);

            _httpClient = _configuration.HttpClient ?? CreateHttpClient();
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
            bool firstTime = true;
            while (ReadyState != ReadyState.Shutdown)
            {
                if (!firstTime)
                {
                    if (_lastSuccessfulConnectionTime.HasValue)
                    {
                        if (DateTime.Now.Subtract(_lastSuccessfulConnectionTime.Value) >= _configuration.BackoffResetThreshold)
                        {
                            _backOff.ResetReconnectAttemptCount();
                        }
                        _lastSuccessfulConnectionTime = null;
                    }
                    await MaybeWaitWithBackOff();
                }
                firstTime = false;

                CancellationTokenSource newRequestTokenSource = null;
                CancellationToken cancellationToken;
                lock (this)
                {
                    if (_readyState == ReadyState.Shutdown)
                    {
                        // in case Close() was called in between the previous ReadyState check and the creation of the new token
                        return;
                    }
                    newRequestTokenSource = new CancellationTokenSource();
                    _currentRequestToken?.Dispose();
                    _currentRequestToken = newRequestTokenSource;
                }

                if (ReadyState == ReadyState.Connecting || ReadyState == ReadyState.Open)
                {
                    throw new InvalidOperationException(string.Format(Resources.EventSource_Already_Started, ReadyState));
                }

                SetReadyState(ReadyState.Connecting);
                cancellationToken = newRequestTokenSource.Token;

                try
                {
                    await ConnectToEventSourceAsync(cancellationToken);

                    // ConnectToEventSourceAsync normally doesn't return, unless it detects that the request has been cancelled.
                    Close(ReadyState.Closed);
                }
                catch (Exception e)
                {
                    CancelCurrentRequest();

                    // If the user called Close(), ReadyState = Shutdown, so errors are irrelevant.
                    if (ReadyState != ReadyState.Shutdown)
                    {
                        Close(ReadyState.Closed);

                        Exception realException = e;
                        if (e is OperationCanceledException oe)
                        {
                            // This exception could either be the result of us explicitly cancelling a request, in which case we don't
                            // need to do anything else, or it could be that the request timed out.
                            if (oe.CancellationToken.IsCancellationRequested)
                            {
                                realException = null;
                            }
                            else
                            {
                                realException = new TimeoutException();
                            }
                        }

                        if (realException != null)
                        {
                            OnError(new ExceptionEventArgs(realException));
                        }
                    }
                }
            }
        }

        private async Task MaybeWaitWithBackOff()  {
            if (_retryDelay.TotalMilliseconds > 0)
            {
                TimeSpan sleepTime = _backOff.GetNextBackOff();
                if (sleepTime.TotalMilliseconds > 0) {
                    _logger.InfoFormat("Waiting {0} milliseconds before reconnecting...", sleepTime.TotalMilliseconds);
                    BackOffDelay = sleepTime;
                    await Task.Delay(sleepTime);
                }
            }
        }

        /// <summary>
        /// Closes the connection to the EventSource API. The EventSource cannot be reopened after this.
        /// </summary>
        public void Close()
        {
            if (ReadyState != ReadyState.Raw && ReadyState != ReadyState.Shutdown)
            {
                Close(ReadyState.Shutdown);
            }
            CancelCurrentRequest();

            // do not dispose httpClient if it is user provided
            if (_configuration.HttpClient == null)
            {
                _httpClient.Dispose();
            }
        }

        /// <summary>
        /// Equivalent to calling <see cref="Close"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                Close();
            }
        }

        #endregion

        #region Private Methods

        private HttpClient CreateHttpClient()
        {
            var client =_configuration.MessageHandler is null ?
                new HttpClient() :
                new HttpClient(_configuration.MessageHandler, false);
            client.Timeout = _configuration.ConnectionTimeout;
            return client;
        }

        private void CancelCurrentRequest()
        {
            CancellationTokenSource requestTokenSource = null;
            lock (this)
            {
                requestTokenSource = _currentRequestToken;
                _currentRequestToken = null;
            }
            if (requestTokenSource != null)
            {
                _logger.Debug("Cancelling current request");
                requestTokenSource.Cancel();
                requestTokenSource.Dispose();
            }
        }

        internal virtual EventSourceService GetEventSourceService(Configuration configuration)
        {
            return new EventSourceService(configuration, _httpClient, _logger);
        }

        private async Task ConnectToEventSourceAsync(CancellationToken cancellationToken)
        {
            _eventBuffer = new List<string>();

            var svc = GetEventSourceService(_configuration);

            svc.ConnectionOpened += (o, e) => {
                _lastSuccessfulConnectionTime = DateTime.Now;
                SetReadyState(ReadyState.Open, OnOpened);
            };
            svc.ConnectionClosed += (o, e) => { SetReadyState(ReadyState.Closed, OnClosed); };

            await svc.GetDataAsync(
                ProcessResponseContent,
                _lastEventId,
                cancellationToken
            );
        }

        private void Close(ReadyState state)
        {
            _logger.DebugFormat("Close({0}) - state was {1}", state, ReadyState);
            SetReadyState(state, OnClosed);
        }
        
        private void ProcessResponseContent(string content)
        {
            if (content == null)
            {
                // StreamReader may emit a null if the stream has been closed; there's nothing to
                // be done at this level in that case
                return;
            }
            if (string.IsNullOrEmpty(content.Trim()))
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
            lock (this)
            {
                if (_readyState == state || _readyState == ReadyState.Shutdown)
                {
                    return;
                }
                _readyState = state;
            }

            if (action != null)
            {
                action(new StateChangedEventArgs(state));
            }
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
            _logger.DebugFormat("Received event \"{0}\"", _eventName);

            OnMessageReceived(new MessageReceivedEventArgs(message, _eventName));

            _eventBuffer.Clear();
            _eventName = Constants.MessageField;
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

        #endregion
    }
}
