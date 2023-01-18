using System;

namespace LaunchDarkly.EventSource.Events
{
    /// <summary>
    /// Represents the beginning of a stream.
    /// </summary>
    /// <remarks>
    /// This event will be returned by <see cref="EventSource.ReadAnyEventAsync"/>
    /// if the stream started as a side effect of calling that method, rather than
    /// from calling <see cref="EventSource.StartAsync"/>. You will also get a new
    /// StartedEvent if the stream was closed and then reconnected.
    /// </remarks>
    public class StartedEvent : IEvent
    {
        /// <inheritdoc/>
        public override bool Equals(object o) =>
            o is StartedEvent;

        /// <inheritdoc/>
        public override int GetHashCode() =>
            typeof(StartedEvent).GetHashCode();
    }
}
