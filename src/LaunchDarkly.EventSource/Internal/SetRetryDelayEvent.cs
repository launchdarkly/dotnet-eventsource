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
    }
}
