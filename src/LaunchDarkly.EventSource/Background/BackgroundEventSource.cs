using System;
using System.Threading.Tasks;
using LaunchDarkly.EventSource.Events;
using LaunchDarkly.EventSource.Exceptions;
using LaunchDarkly.Logging;

namespace LaunchDarkly.EventSource.Background
{
    /// <summary>
    /// A wrapper for <see cref="EventSource"/> that reads the stream on a long-running
    /// asynchronous task, pushing events to event handlers that the caller provides.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Event handlers are called asynchronously from the <see cref="BackgroundEventSource"/>
    /// task, which will await the <see cref="Task"/> that they return before reading another
    /// event. Therefore, if an event handler wants to start long-running operations while
    /// letting the stream continue to receive other events, it should explicitly create a
    /// detached task with <see cref="Task.Run(Func{Task})"/>.
    /// </para>
    /// <para>
    /// This is very similar to the asynchronous model that was used by EventSource prior to
    /// the 6.0.0 release. Code that was written against earlier versions of EventSource
    /// can be adapted to use BackgroundEventSource as follows:
    /// </para>
    /// <example><code>
    /// // before (version 5.x)
    ///     var eventSource = new EventSource(eventSourceConfig);
    ///     eventSource.MessageReceived += myMessageReceivedHandler;
    ///     Task.Run(() => eventSource.StartAsync());
    ///
    /// // after (version 6.x)
    ///     var backgroundEventSource = new BackgroundEventSource(eventSourceConfig);
    ///     backgroundEventSource.MessageReceived += myAsyncMessageReceivedHandler;
    ///     // note that myAsyncMessageReceivedHandler is an async function, unlike the
    ///     // previous myMessageReceivedHandler which was a synchronous void function
    ///     Task.Run(backgroundEventSource.RunAsync);
    /// </code></example>
    /// </remarks>
    public class BackgroundEventSource : IDisposable
    {
        #region Private Fields

        private readonly IEventSource _eventSource;

        #endregion

        #region Public Types

        /// <summary>
        /// Equivalent to <see cref="EventHandler{TEventArgs}"/> but returns a
        /// <see cref="Task"/>, allowing event handlers to perform asynchronous
        /// operations.
        /// </summary>
        /// <typeparam name="T">the event argument type</typeparam>
        /// <param name="sender">the <see cref="BackgroundEventSource"/> instance</param>
        /// <param name="value">the event argument</param>
        /// <returns>a <see cref="Task"/></returns>
        public delegate Task AsyncEventHandler<T>(object sender, T value);

        #endregion

        #region Public Events

        /// <inheritdoc/>
        public event AsyncEventHandler<StateChangedEventArgs> Opened;

        /// <inheritdoc/>
        public event AsyncEventHandler<StateChangedEventArgs> Closed;

        /// <inheritdoc/>
        public event AsyncEventHandler<MessageReceivedEventArgs> MessageReceived;

        /// <inheritdoc/>
        public event AsyncEventHandler<CommentReceivedEventArgs> CommentReceived;

        /// <inheritdoc/>
        public event AsyncEventHandler<ExceptionEventArgs> Error;

        #endregion

        #region Public Properties

        /// <summary>
        /// Returns the underlying <see cref="IEventSource"/> that this
        /// <see cref="BackgroundEventSource"/> is wrapping. This allows access to
        /// properties and methods like <see cref="EventSource.ReadyState"/> and
        /// <see cref="EventSource.Interrupt"/>.
        /// </summary>
        public IEventSource EventSource => _eventSource;

        #endregion

        #region Public Constructors

        /// <summary>
        /// Creates a new instance to wrap an already-constructed <see cref="EventSource"/>.
        /// </summary>
        /// <param name="eventSource">the underlying SSE client</param>
        /// <exception cref="ArgumentNullException">if the parameter is null</exception>
        public BackgroundEventSource(IEventSource eventSource)
        {
            if (eventSource is null)
            {
                throw new ArgumentNullException(nameof(eventSource));
            }
            _eventSource = eventSource;
        }

        /// <summary>
        /// Creates a new instance to wrap a new <see cref="EventSource"/>.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <exception cref="ArgumentNullException">if the parameter is null</exception>
        public BackgroundEventSource(Configuration configuration) :
            this(new EventSource(configuration))
        { }

        #endregion

        #region Public Methods

        /// <summary>
        /// Reads messages and other events from the underlying SSE client and
        /// dispatches them to the event handlers.
        /// </summary>
        /// <returns>an asynchronous task representing the read loop</returns>
        public async Task RunAsync()
        {
            while (_eventSource.ReadyState != ReadyState.Shutdown)
            {
                try
                {
                    var e = await _eventSource.ReadAnyEventAsync();
                    if (e is MessageEvent me)
                    {
                        await InvokeHandler(MessageReceived, new MessageReceivedEventArgs(me));
                    }
                    else if (e is CommentEvent ce)
                    {
                        await InvokeHandler(CommentReceived, new CommentReceivedEventArgs(ce.Text));
                    }
                    else if (e is StartedEvent se)
                    {
                        await InvokeHandler(Opened, new StateChangedEventArgs(_eventSource.ReadyState));
                    }
                    else if (e is FaultEvent fe)
                    {
                        await InvokeErrorHandler(fe.Exception);
                        await InvokeHandler(Closed, new StateChangedEventArgs(_eventSource.ReadyState));
                    }
                }
                catch (Exception ex)
                {
                    await InvokeErrorHandler(ex);
                }
            }
        }

        private async Task InvokeHandler<T>(AsyncEventHandler<T> handler, T args)
        {
            try
            {
                await (handler?.Invoke(this, args) ?? Task.CompletedTask);
            }
            catch (Exception ex)
            {
                _eventSource.Logger.Error(
                    "BackgroundEventSource caught an exception while calling an event handler: {0}",
                    LogValues.ExceptionSummary(ex));
                _eventSource.Logger.Debug(LogValues.ExceptionTrace(ex));
                await InvokeErrorHandler(ex);
            }
        }

        private async Task InvokeErrorHandler(Exception ex)
        {
            if (ex is StreamClosedByCallerException)
            {
                // This exception isn't very useful in the push event model, and didn't have
                // an equivalent in the older EventSource API, so we'll swallow it
                return;
            }
            try
            {
                await (Error?.Invoke(this, new ExceptionEventArgs(ex)) ?? Task.CompletedTask);
            }
            catch (Exception anotherEx)
            {
                _eventSource.Logger.Error(
                    "BackgroundEventSource caught an exception while calling an error handler: {0}",
                    LogValues.ExceptionSummary(anotherEx));
                _eventSource.Logger.Debug(LogValues.ExceptionTrace(anotherEx));
            }
        }

        /// <summary>
        /// Equivalent to calling Dispose on the underlying <see cref="EventSource"/>.
        /// </summary>
        public void Dispose() =>
            Dispose(true);

        #endregion

        #region Private Methods

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _eventSource.Close();
            }
        }

        #endregion
    }

}
