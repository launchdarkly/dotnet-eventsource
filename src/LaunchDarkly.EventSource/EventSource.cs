using System;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.EventSource.Events;
using LaunchDarkly.EventSource.Exceptions;
using LaunchDarkly.EventSource.Internal;
using LaunchDarkly.Logging;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// A client for consuming <see href="https://html.spec.whatwg.org/multipage/server-sent-events.html">Server-Sent
    /// Events.</see>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The client is created in an inactive state. It uses a pull model where the
    /// caller starts the EventSource and then requests data from it, one event at
    /// a time. The initial connection attempt is made when you call <see cref="StartAsync"/>,
    /// or when you first try to read an event.
    /// </para>
    /// <para>
    /// If, instead of requesting events in a loop, you would like to have them
    /// pushed to you using an event handler model, use
    /// <see cref="LaunchDarkly.EventSource.Background.BackgroundEventSource"/>.
    /// </para>
    /// <para>
    /// Note that although EventSource is named after the JavaScript API that is described
    /// in the SSE specification, its behavior is not necessarily identical to standard
    /// web browser implementations of EventSource: it can be configured to automatically
    /// retry (with a backoff delay) for error conditions where a browser will not retry,
    /// and it also supports request configuration options (such as request headers and
    /// method) that the browser EventSource does not support. However, its interpretation
    /// of the stream data is fully conformant with the SSE specification.
    /// </para>
    /// </remarks>
    public class EventSource : IEventSource, IDisposable
    {
        #region Private Fields

        private readonly Configuration _configuration;
        private readonly Logger _logger;
        private readonly ConnectStrategy.Client _client;
        private readonly ErrorStrategy _baseErrorStrategy;
        private readonly RetryDelayStrategy _baseRetryDelayStrategy;
        private readonly TimeSpan _retryDelayResetThreshold;
        private readonly Uri _origin;
        private readonly object _lock = new object();

        private ReadyState _readyState;
        private TimeSpan _baseRetryDelay;
        private TimeSpan? _nextRetryDelay;
        private DateTime? _connectedTime;
        private DateTime? _disconnectedTime;
        private CancellationToken? _cancellationToken;
        private volatile CancellationTokenSource _cancellationTokenSource;
        private volatile IDisposable _requestCloser;

        private EventParser _parser;
        private ErrorStrategy _currentErrorStrategy;
        private RetryDelayStrategy _currentRetryDelayStrategy;
        private volatile string _lastEventId;
        private volatile bool _deliberatelyClosedConnection;

        #endregion

        #region Public Properties

        /// <inheritdoc/>
        public ReadyState ReadyState => WithLock(() => _readyState);

        /// <inheritdoc/>
        public TimeSpan BaseRetryDelay => WithLock(() => _baseRetryDelay);

        /// <inheritdoc/>
        public string LastEventId => _lastEventId;

        /// <inheritdoc/>
        public TimeSpan? NextRetryDelay => WithLock(() => _nextRetryDelay);

        /// <inheritdoc/>
        public Uri Origin => _origin;

        /// <inheritdoc/>
        public Logger Logger => _logger;

        #endregion

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EventSource" /> class.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        public EventSource(Configuration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = _configuration.Logger;

            _client = _configuration.ConnectStrategy.CreateClient(_logger);
            _origin = _configuration.ConnectStrategy.Origin;

            _readyState = ReadyState.Raw;
            _baseErrorStrategy = _currentErrorStrategy = _configuration.ErrorStrategy;
            _baseRetryDelayStrategy = _currentRetryDelayStrategy = _configuration.RetryDelayStrategy;
            _retryDelayResetThreshold = _configuration.RetryDelayResetThreshold;
            _baseRetryDelay = _configuration.InitialRetryDelay;
            _nextRetryDelay = null;
            _lastEventId = _configuration.LastEventId;
            _connectedTime = _disconnectedTime = null;
            _cancellationToken = null;
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
            await TryStartAsync(false);
        }

        /// <inheritdoc/>
        public async Task<MessageEvent> ReadMessageAsync()
        {
            while (true)
            {
                IEvent e = await ReadAnyEventAsync();
                if (e is MessageEvent m)
                {
                    return m;
                }
            }
        }

        /// <inheritdoc/>
        public async Task<IEvent> ReadAnyEventAsync()
        {
            while (true)
            {
                Exception exception = null;

                // Reading an event implies starting the stream if it isn't already started.
                // We might also be restarting since we could have been interrupted at any time.
                if (_parser is null)
                {
                    try
                    {
                        var fault = await TryStartAsync(true);
                        return (IEvent)fault ?? (IEvent)(new StartedEvent());
                    }
                    catch (Exception ex)
                    {
                        exception = ex;
                    }
                }
                if (exception is null)
                {
                    try
                    {
                        var e = await _parser.NextEventAsync();

                        if (e is SetRetryDelayEvent srde)
                        {
                            // SetRetryDelayEvent means the stream contained a "retry:" line. We don't
                            // surface this to the caller, we just apply the new delay and move on.
                            lock (_lock)
                            {
                                _baseRetryDelay = srde.RetryDelay;
                            }
                            _currentRetryDelayStrategy = _baseRetryDelayStrategy;
                            continue;
                        }
                        if (e is MessageEvent me)
                        {
                            if (me.LastEventId != null)
                            {
                                _lastEventId = me.LastEventId;
                            }
                        }
                        return e;
                    }
                    catch (Exception ex)
                    {
                        exception = ex;
                        if (!_deliberatelyClosedConnection)
                        {
                            _logger.Debug("Encountered exception: {0}", LogValues.ExceptionSummary(ex));
                        }
                        // fall through to next block
                    }
                }
                if (_deliberatelyClosedConnection)
                {
                    // If the stream was explicitly closed from another thread, that'll likely show up as
                    // an I/O error or an OperationCanceledException, but we don't want to report it as one.
                    exception = new StreamClosedByCallerException();
                    _deliberatelyClosedConnection = false;
                }
                WithLock(() => _disconnectedTime = DateTime.Now);
                CloseCurrentStream();
                _parser = null;
                ComputeRetryDelay();
                if (ApplyErrorStrategy(exception) == ErrorStrategy.Action.Continue)
                {
                    return new FaultEvent(exception);
                }
                throw exception;
            }
        }

        /// <inheritdoc/>
        public void Interrupt() =>
            CloseCurrentStream();

        /// <inheritdoc/>
        public void Close()
        {
            lock (_lock)
            {
                if (_readyState == ReadyState.Shutdown)
                {
                    return;
                }
                _readyState = ReadyState.Shutdown;
            }
            CloseCurrentStream();
            _client?.Dispose();
        }

        /// <summary>
        /// Equivalent to calling <see cref="Close()"/>.
        /// </summary>
        public void Dispose() =>
            Dispose(true);

        #endregion

        #region Private Methods

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                Close();
            }
        }

        private T WithLock<T>(Func<T> func)
        {
            lock (_lock) { return func(); }
        }

        private async Task<FaultEvent> TryStartAsync(bool canReturnFaultEvent)
        {
            if (_parser != null)
            {
                return null;
            }
            while (true)
            {
                StreamException exception = null;
                TimeSpan delayNow = TimeSpan.Zero;
                lock (_lock)
                {
                    if (_readyState == ReadyState.Shutdown)
                    {
                        throw new StreamClosedByCallerException();
                    }
                    _readyState = ReadyState.Connecting;

                    var nextDelay = _nextRetryDelay ?? TimeSpan.Zero;
                    if (nextDelay > TimeSpan.Zero)
                    {
                        delayNow = _disconnectedTime.HasValue ?
                            (nextDelay - (DateTime.Now - _disconnectedTime.Value)) :
                            nextDelay;
                    }
                }
                if (delayNow > TimeSpan.Zero)
                {
                    _logger.Info("Waiting {0} milliseconds before reconnecting", delayNow.TotalMilliseconds);
                    await Task.Delay(delayNow);
                }

                ConnectStrategy.Client.Result connectResult = null;
                CancellationToken newCancellationToken;
                if (exception is null)
                {
                    CancellationTokenSource newRequestTokenSource = new CancellationTokenSource();
                    lock (_lock)
                    {
                        if (_readyState == ReadyState.Shutdown)
                        {
                            // in case Close() was called since the last time we checked
                            return null;
                        }

                        _connectedTime = null;
                        _deliberatelyClosedConnection = false;

                        _cancellationTokenSource?.Dispose();
                        _cancellationTokenSource = newRequestTokenSource;
                        _cancellationToken = newRequestTokenSource.Token;
                    }
                    try
                    {
                        connectResult = await _client.ConnectAsync(
                            new ConnectStrategy.Client.Params
                            {
                                CancellationToken = newRequestTokenSource.Token,
                                LastEventId = _lastEventId
                            });
                    }
                    catch (StreamException e)
                    {
                        exception = e;
                    }
                }

                if (exception != null)
                {
                    lock (_lock)
                    {
                        if (_readyState == ReadyState.Shutdown)
                        {
                            return null;
                        }
                        _readyState = ReadyState.Closed;
                        _disconnectedTime = DateTime.Now;
                        ComputeRetryDelay();
                    }
                    _logger.Debug("Encountered exception: {0}", LogValues.ExceptionSummary(exception));
                    if (ApplyErrorStrategy(exception) == ErrorStrategy.Action.Continue)
                    {
                        // The ErrorStrategy told us to CONTINUE rather than throwing an exception.
                        if (canReturnFaultEvent)
                        {
                            return new FaultEvent(exception);
                        }
                        // If canReturnFaultEvent is false, it means the caller explicitly called start(),
                        // in which case there's no way to return a FaultEvent so we just keep retrying
                        // transparently.
                        continue;
                    }
                    // The ErrorStrategy told us to THROW rather than CONTINUE. 
                    throw exception;
                }

                lock (_lock)
                {
                    _connectedTime = DateTime.Now;
                    _readyState = ReadyState.Open;
                    _requestCloser = connectResult;
                }
                _logger.Debug("Connected to SSE stream");

                _parser = new EventParser(
                    connectResult.Stream,
                    connectResult.ReadTimeout ?? Timeout.InfiniteTimeSpan,
                    _origin,
                    newCancellationToken,
                    _logger
                    );

                _currentErrorStrategy = _baseErrorStrategy;
                return null;
            }
        }

        private ErrorStrategy.Action ApplyErrorStrategy(Exception exception)
        {
            var result = _currentErrorStrategy.Apply(exception);
            _currentErrorStrategy = result.Next ?? _currentErrorStrategy;
            return result.Action;
        }

        private void ComputeRetryDelay()
        {
            lock (_lock)
            {
                if (_retryDelayResetThreshold > TimeSpan.Zero && _connectedTime.HasValue)
                {
                    TimeSpan connectionDuration = DateTime.Now.Subtract(_connectedTime.Value);
                    if (connectionDuration >= _retryDelayResetThreshold)
                    {
                        _currentRetryDelayStrategy = _baseRetryDelayStrategy;
                    }
                    var result = _currentRetryDelayStrategy.Apply(_baseRetryDelay);
                    _nextRetryDelay = result.Delay;
                    _currentRetryDelayStrategy = result.Next ?? _currentRetryDelayStrategy;
                }
            }
        }

        private void CloseCurrentStream()
        {
            CancellationTokenSource oldTokenSource;
            IDisposable oldRequestCloser;
            lock (_lock)
            {
                if (_cancellationTokenSource is null)
                {
                    return;
                }
                _disconnectedTime = DateTime.Now;
                oldTokenSource = _cancellationTokenSource;
                oldRequestCloser = _requestCloser;
                _cancellationTokenSource = null;
                _requestCloser = null;
                _deliberatelyClosedConnection = true;
                if (_readyState != ReadyState.Shutdown)
                {
                    _readyState = ReadyState.Closed;
                }
            }
            _logger.Debug("Cancelling current request");
            oldTokenSource.Cancel();
            oldTokenSource.Dispose();
            oldRequestCloser?.Dispose();
        }

        #endregion
    }
}
