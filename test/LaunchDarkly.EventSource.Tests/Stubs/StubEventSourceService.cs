using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LaunchDarkly.EventSource.Tests.Stubs
{
    internal class StubEventSourceService : EventSourceService
    {
        private readonly int _delayedResponseInMilliseconds;

        public StubEventSourceService(Configuration config, int delayedResponseInMilliseconds) : base(config)
        {
            _delayedResponseInMilliseconds = delayedResponseInMilliseconds;
        }

        protected override IStreamReader GetStreamReader(Stream stream)
        {
            return new StubEventSourceStreamReader(stream, _delayedResponseInMilliseconds);
        }
    }
}
