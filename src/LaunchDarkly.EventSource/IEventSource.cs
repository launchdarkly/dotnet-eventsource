using System;
using System.Threading.Tasks;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// This interface defines the public members of <see cref="EventSource"/>.
    /// </summary>
    public interface IEventSource
    {
        #region Public Events

        /// <summary>
        /// Occurs when the connection to the EventSource API has been opened.
        /// </summary>
        event EventHandler<StateChangedEventArgs> Opened;
        /// <summary>
        /// Occurs when the connection to the EventSource API has been closed.
        /// </summary>
        event EventHandler<StateChangedEventArgs> Closed;
        /// <summary>
        /// Occurs when a Server Sent Event from the EventSource API has been received.
        /// </summary>
        event EventHandler<MessageReceivedEventArgs> MessageReceived;
        /// <summary>
        /// Occurs when a comment has been received from the EventSource API.
        /// </summary>
        event EventHandler<CommentReceivedEventArgs> CommentReceived;
        /// <summary>
        /// Occurs when an error has happened when the EventSource is open and processing Server Sent Events.
        /// </summary>
        event EventHandler<ExceptionEventArgs> Error;

        #endregion Public Events

        #region Public Properties

        /// <summary>
        /// Gets the state of the EventSource connection.
        /// </summary>
        ReadyState ReadyState { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initiates a connection to the SSE server and begins parsing events.
        /// </summary>
        /// <returns>a <see cref="Task"/> that will be completed only when the
        /// <c>EventSource</c> is closed</returns>
        /// <exception cref="InvalidOperationException">if the method was called again after the
        /// stream connection was already active</exception>
        Task StartAsync();

        /// <summary>
        /// Triggers the same "close and retry" behavior as if an error had been encountered on the stream.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the stream is currently active, this closes the connection, waits for some amount of time
        /// as determined by the usual backoff behavior (and <paramref name="resetBackoffDelay"/>), and
        /// then attempts to reconnect. If the stream is not yet connected, is already waiting to
        /// reconnect, or has been permanently shut down, this has no effect.
        /// </para>
        /// <para>
        /// The method returns immediately without waiting for the reconnection to happen. You will
        /// receive <see cref="Closed"/> and <see cref="Opened"/> events when it does happen (or an
        /// <see cref="Error"/> event if the new connection attempt fails).
        /// </para>
        /// </remarks>
        /// <param name="resetBackoffDelay">true if the delay before reconnection should be reset to
        /// the lowest level of the currently active bounds, which could be the temporary bounds if
        /// <see cref="SetTemporaryRetryDelayBounds(TimeSpan, TimeSpan)"/> is in effect; false if it
        /// should increase according to the usual exponential backoff logic</param>
        void Restart(bool resetBackoffDelay);

        /// <summary>
        /// Temporarily replaces the bounds used to compute reconnection delays.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The configured <see cref="ConfigurationBuilder.InitialRetryDelay(TimeSpan)"/> and
        /// <see cref="ConfigurationBuilder.MaxRetryDelay(TimeSpan)"/> normally govern the
        /// exponential backoff between reconnection attempts. This method installs different
        /// bounds at runtime, so a caller can slow reconnection down without reconfiguring or
        /// recreating the <c>EventSource</c>.
        /// </para>
        /// <para>
        /// The bounds are <i>temporary</i>: they remain in effect until either a connection stays
        /// open for at least
        /// <see cref="ConfigurationBuilder.BackoffResetThreshold(TimeSpan)"/>, at which point the
        /// configured bounds are restored automatically, or
        /// <see cref="ClearTemporaryRetryDelayBounds"/> is called.
        /// </para>
        /// <para>
        /// Changing the bounds resets the backoff level, so the next delay is computed from the new
        /// <paramref name="initialDelay"/> rather than continuing to grow from where it left off. 
        /// Calling this with bounds that are already in effect does nothing, so it is safe to call
        /// repeatedly.
        /// </para>
        /// <para>
        /// Negative values are changed to zero. If <paramref name="initialDelay"/> is greater than
        /// <paramref name="maxDelay"/>, delays are limited to <paramref name="maxDelay"/>. No
        /// argument causes this method to throw, and no argument can cause a later reconnect to
        /// fail.
        /// </para>
        /// <para>
        /// A server-directed reconnection time received via the SSE <c>retry:</c> field supersedes
        /// <paramref name="initialDelay"/>, and the configured
        /// <see cref="ConfigurationBuilder.InitialRetryDelay(TimeSpan)"/> along with it. Such a
        /// value does not affect <paramref name="maxDelay"/>, so the ceiling set here continues to
        /// bound reconnection.
        /// </para>
        /// <para>
        /// It is safe to call this from any thread, including from an
        /// <see cref="Error"/> handler.
        /// </para>
        /// </remarks>
        /// <param name="initialDelay">the lowest delay to use while these bounds are in effect,
        /// unless a server-directed <c>retry:</c> value supersedes it</param>
        /// <param name="maxDelay">the highest delay to use while these bounds are in effect</param>
        /// <seealso cref="ClearTemporaryRetryDelayBounds"/>
        void SetTemporaryRetryDelayBounds(TimeSpan initialDelay, TimeSpan maxDelay);

        /// <summary>
        /// Discards any bounds installed by
        /// <see cref="SetTemporaryRetryDelayBounds(TimeSpan, TimeSpan)"/>, restoring the configured
        /// <see cref="ConfigurationBuilder.MaxRetryDelay(TimeSpan)"/> and, unless a server-directed
        /// <c>retry:</c> value is in effect, the configured
        /// <see cref="ConfigurationBuilder.InitialRetryDelay(TimeSpan)"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Restoring the configured bounds resets the backoff level. If no temporary bounds
        /// are in effect this does nothing. The count of reconnection attempts is not reset.
        /// Calling this is not required as the configured bounds are also restored automatically
        /// after a connection stays open for
        /// <see cref="ConfigurationBuilder.BackoffResetThreshold(TimeSpan)"/>.
        /// </para>
        /// <para>
        /// A server-directed reconnection time received via the SSE <c>retry:</c> field is not
        /// discarded and continues to supersede
        /// <see cref="ConfigurationBuilder.InitialRetryDelay(TimeSpan)"/>, so the configured
        /// minimum may not be the one actually in use afterwards. Only a later <c>retry:</c> value
        /// replaces it. <see cref="ConfigurationBuilder.MaxRetryDelay(TimeSpan)"/> is unaffected
        /// and is always restored.
        /// </para>
        /// </remarks>
        /// <seealso cref="SetTemporaryRetryDelayBounds(TimeSpan, TimeSpan)"/>
        void ClearTemporaryRetryDelayBounds();

        /// <summary>
        /// Closes the connection to the SSE server. The <c>EventSource</c> cannot be reopened after this.
        /// </summary>
        void Close();

        #endregion
    }
}
