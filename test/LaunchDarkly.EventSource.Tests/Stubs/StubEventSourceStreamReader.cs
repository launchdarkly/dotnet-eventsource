using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LaunchDarkly.EventSource.Tests.Stubs
{
    internal class StubEventSourceStreamReader : EventSourceStreamReader
    {
        private readonly int _delayedResponseInMilliseconds;

        public StubEventSourceStreamReader(Stream stream, int delayedResponseInMilliseconds) : base(stream)
        {
            _delayedResponseInMilliseconds = delayedResponseInMilliseconds;
        }

        public override async Task<string> ReadLineAsync()
        {
            await Task.Delay(_delayedResponseInMilliseconds);

            return await base.ReadLineAsync();
        }
    }
}
