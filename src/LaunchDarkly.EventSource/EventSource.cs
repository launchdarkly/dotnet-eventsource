using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.Logging;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// Provides an EventSource client for consuming Server-Sent Events. Additional details on the Server-Sent Events spec
    /// can be found at https://html.spec.whatwg.org/multipage/server-sent-events.html
    /// </summary>
    public class EventSource : IEventSource, IDisposable
    {
        #region Private Fields

        private readonly Configuration _configuration;
        private readonly HttpClient _httpClient;
        private readonly Logger _logger;

        private List<string> _eventDataStringBuffer;
        private MemoryStream _eventDataUtf8ByteBuffer;
        private string _eventName;
        private string _lastEventId;
        private TimeSpan _retryDelay;
        private readonly ExponentialBackoffWithDecorrelation _backOff;
        private CancellationTokenSource _currentRequestToken;
        private DateTime? _lastSuccessfulConnectionTime;
        private ReadyState _readyState;

        #endregion

        #region Public Events

        /// <inheritdoc/>
        public event EventHandler<StateChangedEventArgs> Opened;

        /// <inheritdoc/>
        public event EventHandler<StateChangedEventArgs> Closed;

        /// <inheritdoc/>
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        /// <inheritdoc/>
        public event EventHandler<CommentReceivedEventArgs> CommentReceived;

        /// <inheritdoc/>
        public event EventHandler<ExceptionEventArgs> Error;

        /// <inheritdoc/>
        public event EventHandler<HttpRequestEventArgs> OnHttpRequest;

        #endregion Public Events

        #region Public Properties

        /// <inheritdoc/>
        public ReadyState ReadyState
        {
            get
            {
                lock (this)
                {
                    return _readyState;
                }
            }
            private set
            {
                lock (this)
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
        /// <param name="configuration">the configuration</param>
        public EventSource(Configuration configuration)
        {
            _readyState = ReadyState.Raw;

            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _logger = _configuration.Logger;

            _retryDelay = _configuration.InitialRetryDelay;

            _backOff = new ExponentialBackoffWithDecorrelation(_retryDelay, _configuration.MaxRetryDelay);

            _httpClient = _configuration.HttpClient ?? CreateHttpClient();
        }

        /// <summary>
        /// Shortcut for initializing an <see cref="EventSource"/> with only a stream URI
        /// and no custom properties.
        /// </summary>
        /// <param name="uri">the stream URI</param>
        /// <exception cref="ArgumentNullException">if the URI is null</exception>
        public EventSource(Uri uri) : this(Configuration.Builder(uri).Build()) {}

        #endregion

        #region Public Methods

        /// <inheritdoc/>
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
                    throw new InvalidOperationException(string.Format(Resources.ErrorAlreadyStarted, ReadyState));
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
                        Exception realException = e;
                        if (realException is AggregateException ae && ae.InnerExceptions.Count == 1)
                        {
                            realException = ae.InnerException;
                        }
                        if (realException is OperationCanceledException oe)
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
                        Close(ReadyState.Closed);
                    }
                }
            }
        }

        private async Task MaybeWaitWithBackOff()  {
            if (_retryDelay.TotalMilliseconds > 0)
            {
                TimeSpan sleepTime = _backOff.GetNextBackOff();
                if (sleepTime.TotalMilliseconds > 0) {
                    _logger.Info("Waiting {0} milliseconds before reconnecting...", sleepTime.TotalMilliseconds);
                    BackOffDelay = sleepTime;
                    await Task.Delay(sleepTime);
                }
            }
        }

        /// <inheritdoc/>
        public void Restart(bool resetBackoffDelay)
        {
            lock (this)
            {
                if (_readyState != ReadyState.Open)
                {
                    return;
                }
                if (resetBackoffDelay)
                {
                    _backOff.ResetReconnectAttemptCount();
                }
            }
            CancelCurrentRequest();
        }

        /// <summary>
        /// Closes the connection to the SSE server. The <c>EventSource</c> cannot be reopened after this.
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
        /// Equivalent to calling <see cref="Close()"/>.
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
            var client =_configuration.HttpMessageHandler is null ?
                new HttpClient() :
                new HttpClient(_configuration.HttpMessageHandler, false);
            client.Timeout = _configuration.ResponseStartTimeout;
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
            _eventDataStringBuffer = null;
            _eventDataUtf8ByteBuffer = null;

            var svc = GetEventSourceService(_configuration);

            
            svc.OnHttpRequest += (o, e) => {
                RaiseOnHttpRequest(e);
            };
            svc.ConnectionOpened += (o, e) => {
                _lastSuccessfulConnectionTime = DateTime.Now;
                SetReadyState(ReadyState.Open, OnOpened);
            };
            svc.ConnectionClosed += (o, e) => { SetReadyState(ReadyState.Closed, OnClosed); };

            await svc.GetDataAsync(
                ProcessResponseLineString,
                ProcessResponseLineUtf8,
                _lastEventId,
                cancellationToken
            );
        }

        private void Close(ReadyState state)
        {
            _logger.Debug("Close({0}) - state was {1}", state, ReadyState);
            SetReadyState(state, OnClosed);
        }
        
        private void ProcessResponseLineString(string content)
        {
            if (content == null)
            {
                // StreamReader may emit a null if the stream has been closed; there's nothing to
                // be done at this level in that case
                return;
            }
            if (content.Length == 0)
            {
                DispatchEvent();
            }
            else
            {
                HandleParsedLine(EventParser.ParseLineString(content));
            }
        }

        private void ProcessResponseLineUtf8(Utf8ByteSpan content)
        {
            if (content.Length == 0)
            {
                DispatchEvent();
            }
            else
            {
                HandleParsedLine(EventParser.ParseLineUtf8Bytes(content));
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

        private void HandleParsedLine(EventParser.Result result)
        {
            if (result.IsComment)
            {
                OnCommentReceived(new CommentReceivedEventArgs(result.GetValueAsString()));
            }
            else if (result.IsDataField)
            {
                if (result.ValueString != null)
                {
                    if (_eventDataStringBuffer is null)
                    {
                        _eventDataStringBuffer = new List<string>(2);
                    }
                    _eventDataStringBuffer.Add(result.ValueString);
                    _eventDataStringBuffer.Add("\n");
                }
                else
                {
                    if (_eventDataUtf8ByteBuffer is null)
                    {
                        _eventDataUtf8ByteBuffer = new MemoryStream(result.ValueBytes.Length + 1);
                    }
                    _eventDataUtf8ByteBuffer.Write(result.ValueBytes.Data, result.ValueBytes.Offset, result.ValueBytes.Length);
                    _eventDataUtf8ByteBuffer.WriteByte((byte)'\n');
                }
            }
            else if (result.IsEventField)
            {
                _eventName = result.GetValueAsString();
            }
            else if (result.IsIdField)
            {
                _lastEventId = result.GetValueAsString();
            }
            else if (result.IsRetryField)
            {
                if (long.TryParse(result.GetValueAsString(), out var retry))
                {
                    _retryDelay = TimeSpan.FromMilliseconds(retry);
                }
            }
        }

        private void DispatchEvent()
        {
            var name = _eventName ?? Constants.MessageField;
            _eventName = null;
            MessageEvent message;

            if (_eventDataStringBuffer != null)
            {
                if (_eventDataStringBuffer.Count == 0)
                {
                    return;
                }
                // remove last item which is always a trailing newline
                _eventDataStringBuffer.RemoveAt(_eventDataStringBuffer.Count - 1);
                var dataString = string.Concat(_eventDataStringBuffer);
                message = new MessageEvent(name, dataString, _lastEventId, _configuration.Uri);

                _eventDataStringBuffer.Clear();
            }
            else
            {
                if (_eventDataUtf8ByteBuffer is null || _eventDataUtf8ByteBuffer.Length == 0)
                {
                    return;
                }
                var dataSpan = new Utf8ByteSpan(_eventDataUtf8ByteBuffer.GetBuffer(), 0,
                    (int)_eventDataUtf8ByteBuffer.Length - 1); // remove trailing newline
                message = new MessageEvent(name, dataSpan, _lastEventId, _configuration.Uri);

                // We've now taken ownership of the original buffer; null out the previous
                // reference to it so a new one will be created next time
                _eventDataUtf8ByteBuffer = null;
            }

            _logger.Debug("Received event \"{0}\"", name);
            OnMessageReceived(new MessageReceivedEventArgs(message));
        }

        private void RaiseOnHttpRequest(HttpRequestEventArgs e) =>
            OnHttpRequest?.Invoke(this, e);

        private void OnOpened(StateChangedEventArgs e) =>
            Opened?.Invoke(this, e);

        private void OnClosed(StateChangedEventArgs e) =>
            Closed?.Invoke(this, e);

        private void OnMessageReceived(MessageReceivedEventArgs e) =>
            MessageReceived?.Invoke(this, e);

        private void OnCommentReceived(CommentReceivedEventArgs e) =>
            CommentReceived?.Invoke(this, e);

        private void OnError(ExceptionEventArgs e) =>
            Error?.Invoke(this, e);

        #endregion
    }
}
