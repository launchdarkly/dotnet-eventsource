using System;
using System.Drawing;
using LaunchDarkly.EventSource.Exceptions;

using static System.Net.Mime.MediaTypeNames;

namespace LaunchDarkly.EventSource.Events
{
    /// <summary>
    /// Describes a failure in the stream.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When an error occurs, if the configured <see cref="ErrorStrategy"/> returns
    /// <see cref="ErrorStrategy.Action.Continue"/>, <see cref="EventSource.ReadAnyEventAsync"/>
    /// will return a FaultEvent. Otherwise, the error would instead be thrown as a
    /// <see cref="StreamException"/>.
    /// </para>
    /// <para>
    /// If you receive a FaultEvent, the EventSource is now in an inactive state since
    /// either a connection attempt has failed or an existing connection has been closed.
    /// EventSource will attempt to reconnect if you either call <see cref="EventSource.StartAsync"/>
    /// or simply continue reading events after this point.
    /// </para>
    /// </remarks>
    public class FaultEvent : IEvent
    {
        /// <summary>
        /// The cause of the failure.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Creates an instance.
        /// </summary>
        /// <param name="exception">the cause of the failure </param>
        public FaultEvent(Exception exception) { Exception = exception; }

        /// <inheritdoc/>
        public override bool Equals(object o) =>
            o is FaultEvent of && object.Equals(Exception, of.Exception);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            Exception?.GetHashCode() ?? 0;

        /// <inheritdoc/>
        public override string ToString() =>
            string.Format("FaultEvent({0})", Exception);
    }
}
