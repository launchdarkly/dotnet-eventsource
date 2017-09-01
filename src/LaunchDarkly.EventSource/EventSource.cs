using Microsoft.Extensions.Logging;
using Polly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
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

        private List<string> _eventBuffer;
        private string _eventName = Constants.MessageField;
        private string _lastEventId;
        private TimeSpan _retryDelay;
        private CancellationTokenSource _pendingRequest;
        private readonly Policy _retryPolicy;
        private readonly ExponentialBackoffWithDecorrelation _backOff;

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
            ReadyState = ReadyState.Raw;

            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _logger = _configuration.Logger ?? new LoggerFactory().CreateLogger<EventSource>();

            _pendingRequest = new CancellationTokenSource();

            _retryDelay = _configuration.DelayRetryDuration;

            _backOff = new ExponentialBackoffWithDecorrelation(_retryDelay.TotalMilliseconds,
                _configuration.MaximumDelayRetryDuration.TotalMilliseconds);

            _retryPolicy = Policy
                .Handle<Exception>(ex => !(ex is EventSourceServiceCancelledException))
                .WaitAndRetryForeverAsync(
                    GetDecorrelatedWaitDuration,
                    (exception, calculatedWaitDuration) =>
                    {
                        _logger.LogInformation(Resources.EventSource_Logger_Disconnected, calculatedWaitDuration.TotalMilliseconds, exception);
                    });

        }

        #endregion

        #region Public Methods        
        /// <summary>
        /// Internal method that allows for a Polly Policy to be injected.
        /// </summary>
        /// <param name="policy">The policy.</param>
        /// <returns></returns>
        internal async Task StartAsync(Policy policy)
        {
            if (policy == null)
            {
                throw new ArgumentNullException(nameof(policy));
            }

            var cancellationToken = _pendingRequest.Token;

            while (!cancellationToken.IsCancellationRequested)
            {
                await policy.ExecuteAsync(async token =>
                {
                    await ConnectToEventSourceAsync(token);

                }, cancellationToken);
            }
        }

        /// <summary>
        /// Initiates the request to the EventSource API and parses Server Sent Events received by the API.
        /// </summary>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> A task that represents the work queued to execute in the ThreadPool.</returns>
        /// <exception cref="InvalidOperationException">The method was called after the connection <see cref="ReadyState"/> was Open or Connecting.</exception> 
        public async Task StartAsync()
        {
            await StartAsync(_retryPolicy);
        }

        /// <summary>
        /// Closes the connection to the EventSource API.
        /// </summary>
        public void Close()
        {
            if (ReadyState == ReadyState.Raw || ReadyState == ReadyState.Shutdown) return;
            
            Close(ReadyState.Shutdown);

            // Cancel token to cancel requests.
            CancelToken();

        }

        #endregion

        #region Private Methods

        private TimeSpan GetDecorrelatedWaitDuration(int retryAttempt)
        {
            // Using a Decorrelated Jitter - adapted from https://www.awsarchitectureblog.com/2015/03/backoff.html
            BackOffDelay = _backOff.GetBackOff();

            return BackOffDelay;

        }
        
        private void CancelToken()
        {
            CancellationTokenSource cancellationTokenSource = Interlocked.Exchange(ref _pendingRequest, new CancellationTokenSource());
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        private async Task ConnectToEventSourceAsync(CancellationToken cancellationToken)
        {
            if (ReadyState == ReadyState.Connecting || ReadyState == ReadyState.Open)
            {
                var errorMessage = string.Format(Resources.EventSource_Already_Started, ReadyState);
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            SetReadyState(ReadyState.Connecting);

            try
            {
                _eventBuffer = new List<string>();

                EventSourceService svc = new EventSourceService(_configuration);

                svc.ConnectionOpened += (o, e) => { SetReadyState(ReadyState.Open, OnOpened); };
                svc.ConnectionClosed += (o, e) => { SetReadyState(ReadyState.Closed, OnClosed); };

                await svc.GetDataAsync(
                    ProcessResponseContent,
                    cancellationToken
                );
            }
            catch (EventSourceServiceCancelledException e)
            {
                CancelToken();

                CloseAndRaiseError(e);
            }
            catch (Exception e)
            {
                // If the user called Close(), ReadyState = Shutdown. Don't rethrow.
                if (ReadyState != ReadyState.Shutdown)
                {
                    CloseAndRaiseError(e);

                    throw;
                }

            }
        }

        private void Close(ReadyState state)
        {
            SetReadyState(state, OnClosed);

            _logger.LogInformation(Resources.EventSource_Logger_Closed);
        }

        private void CloseAndRaiseError(Exception ex)
        {
            Close(ReadyState.Closed);

            OnError(new ExceptionEventArgs(ex));
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
