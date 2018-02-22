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
        /// <value>
        /// One of the <see cref="EventSource.ReadyState"/> values, which represents the state of the EventSource connection.
        /// </value>
        ReadyState ReadyState
        {
            get;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initiates the request to the EventSource API and parses Server Sent Events received by the API.
        /// </summary>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> A task that represents the work queued to execute in the ThreadPool.</returns>
        /// <exception cref="InvalidOperationException">The method was called after the connection <see cref="ReadyState"/> was Open or Connecting.</exception>
        Task StartAsync();

        /// <summary>
        /// Closes the connection to the EventSource API.
        /// </summary>
        void Close();

        #endregion
    }
}
