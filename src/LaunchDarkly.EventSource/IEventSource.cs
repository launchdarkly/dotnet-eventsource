using System;
using System.Threading.Tasks;
using LaunchDarkly.EventSource.Events;
using LaunchDarkly.EventSource.Exceptions;
using LaunchDarkly.Logging;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// This interface defines the public members of <see cref="EventSource"/>.
    /// </summary>
    public interface IEventSource
    {
        #region Public Properties

        /// <summary>
        /// Gets the state of the EventSource connection.
        /// </summary>
        ReadyState ReadyState { get; }

        /// <summary>
        /// Returns the current base retry delay.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is initially set by <see cref="ConfigurationBuilder.InitialRetryDelay(TimeSpan)"/>,
        /// or <see cref="Configuration.DefaultInitialRetryDelay"/> if not specified.
        /// It can be overridden by the stream provider if the stream contains a
        /// "retry:" line.
        /// </para>
        /// <para>
        /// The actual retry delay for any given reconnection is computed by applying the
        /// backoff/jitter algorithm to this value.
        /// </para>
        /// </remarks>
        TimeSpan BaseRetryDelay { get; }

        /// <summary>
        /// The ID value, if any, of the last known event.
        /// </summary>
        /// <remarks>
        /// This can be set initially with <see cref="ConfigurationBuilder.LastEventId(string)"/>,
        /// and is updated whenever an event is received that has an ID. Whether event IDs
        /// are supported depends on the server; it may ignore this value.
        /// </remarks>
        string LastEventId { get; }

        /// <summary>
        /// The retry delay that will be used for the next reconnection, if the
        /// stream has failed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If you have just received a <see cref="StreamException"/> or
        /// <see cref="FaultEvent"/>, this value tells you how long EventSource will
        /// sleep before reconnecting, if you tell it to reconnect by calling
        /// <see cref="StartAsync"/> or by trying to read another event. The value
        /// is computed by applying the backoff/jitter algorithm to the current
        /// value of <see cref="BaseRetryDelay"/>. If there has not been a stream
        /// failure, the value is null.
        /// </para>
        /// </remarks>
        TimeSpan? NextRetryDelay { get; }

        /// <summary>
        /// The stream URI.
        /// </summary>
        Uri Origin { get; }

        /// <summary>
        /// The configured logging destination.
        /// </summary>
        Logger Logger { get; }

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
        /// Attempts to receive a message from the stream.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the stream is not already active, this calls <see cref="StartAsync"/> to
        /// establish a connection.
        /// </para>
        /// <para>
        /// As long as the stream is active, the method waits until a message is available.
        /// If the stream fails, the default behavior is to throw a <see cref="StreamException"/>,
        /// but you can configure an <see cref="ErrorStrategy"/> to allow the client to retry
        /// transparently instead. However, the client will never retry if you called
        /// <see cref="Close"/> or <see cref="IDisposable.Dispose"/>; in that case, trying to
        /// read will always throw a <see cref="StreamClosedByCallerException"/>.
        /// </para>
        /// <para>
        /// This method must be called from the same thread that first started using the
        /// stream (that is, the thread that called <see cref="StartAsync"/> or read the
        /// first event).
        /// </para>
        /// </remarks>
        /// <returns>an SSE message</returns>
        /// <seealso cref="ReadAnyEventAsync"/>
        Task<MessageEvent> ReadMessageAsync();

        /// <summary>
        /// Attempts to receive an event of any kind from the stream.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is similar to <see cref="ReadMessageAsync"/>, except that instead of
        /// specifically requesting a <see cref="MessageEvent"/> it also applies to the
        /// other <see cref="IEvent"/> classes: <see cref="StartedEvent"/>,
        /// <see cref="FaultEvent"/>, and <see cref="CommentEvent"/>. Use this method
        /// if you want to be informed of any of those occurrences.
        /// </para>
        /// <para>
        /// The error behavior is the same as <see cref="ReadMessageAsync"/>, except
        /// that if the <see cref="ErrorStrategy"/> is configured to let the client
        /// continue, you will receive a <see cref="FaultEvent"/> describing the error
        /// first, and then a <see cref="StartedEvent"/> once the stream is reconnected.
        /// However, the client will never retry if you called <see cref="Close"/> or
        /// <see cref="IDisposable.Dispose"/>; in that case, trying to read will always
        /// throw a <see cref="StreamClosedByCallerException"/>.
        /// </para>
        /// <para>
        /// This method must be called from the same thread that first started using the
        /// stream (that is, the thread that called <see cref="StartAsync"/> or read the
        /// first event).
        /// </para>
        /// </remarks>
        /// <returns>an event</returns>
        Task<IEvent> ReadAnyEventAsync();

        /// <summary>
        /// Stops the stream connection if it is currently active.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Unlike the reading methods, you are allowed to call this method from any
        /// thread. If you are reading events on a different thread, and automatic
        /// retries are not enabled by an <see cref="ErrorStrategy"/>, the other thread
        /// will receive a <see cref="StreamClosedByCallerException"/>.
        /// </para>
        /// <para>
        /// Calling <see cref="StartAsync"/> or trying to read more events after this
        /// will cause the stream to reconnect, using the same retry delay logic as if
        /// the stream had been closed due to an error.
        /// </para>
        /// </remarks>
        void Interrupt();

        /// <summary>
        /// Closes the connection to the SSE server. The <c>EventSource</c> cannot be reopened after this.
        /// </summary>
        void Close();

        #endregion
    }
}
