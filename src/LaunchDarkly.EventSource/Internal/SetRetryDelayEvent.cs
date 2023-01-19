using System;
using LaunchDarkly.EventSource.Events;

namespace LaunchDarkly.EventSource.Internal
{
    internal class SetRetryDelayEvent : IEvent
    {
        public TimeSpan RetryDelay { get; }

        public SetRetryDelayEvent(TimeSpan retryDelay)
        {
            RetryDelay = retryDelay;
        }

        public override bool Equals(object obj) =>
            obj is SetRetryDelayEvent srde && srde.RetryDelay == RetryDelay;

        public override int GetHashCode() => RetryDelay.GetHashCode();
    }
}
