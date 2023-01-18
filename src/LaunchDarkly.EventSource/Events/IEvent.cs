using System;

namespace LaunchDarkly.EventSource.Events
{
    /// <summary>
    /// A marker interface for all types of stream information that can be returned by
    /// <see cref="EventSource.ReadAnyEventAsync"/>.
    /// </summary>
    public interface IEvent
    {
    }
}
