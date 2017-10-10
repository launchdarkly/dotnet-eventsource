using System;
using System.Collections.Generic;
using System.Text;

namespace LaunchDarkly.EventSource.Tests.Stubs
{
    public class StubEventSource : EventSource
    {
        private readonly int _delayedResponseInMilliseconds;

        public StubEventSource(Configuration configuration, int delayedResponseInMilliseconds) : base(configuration)
        {
            _delayedResponseInMilliseconds = delayedResponseInMilliseconds;
        }

        internal override EventSourceService GetEventSourceService(Configuration configuration)
        {
            return new StubEventSourceService(configuration, _delayedResponseInMilliseconds);
        }
    }
}
