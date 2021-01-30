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
        /// the lowest level (<see cref="ConfigurationBuilder.InitialRetryDelay(TimeSpan)"/>); false if it
        /// should increase according to the usual exponential backoff logic</param>
        void Restart();

        /// <summary>
        /// Closes the connection to the SSE server. The <c>EventSource</c> cannot be reopened after this.
        /// </summary>
        void Close();

        #endregion
    }
}
