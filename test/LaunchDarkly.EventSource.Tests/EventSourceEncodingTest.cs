using System;
using System.Text;
using System.Threading.Tasks;
using LaunchDarkly.EventSource.Events;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.EventSource.TestHelpers;

namespace LaunchDarkly.EventSource
{
    public class EventSourceEncodingTest : BaseTest
    {
        public EventSourceEncodingTest(ITestOutputHelper testOutput) : base(testOutput) { }

        private static readonly string[] streamChunks = new string[]
        {
            ": hello\n",
            "data: value1",
            "\n\nevent: event2\ndata:",
            "ça\ndata: qu",
            "é\n",
            "\n",
            "data:" + MakeLongString(0, 500),
            MakeLongString(500, 1000) + "\n\n"
        };

        private static async Task ExpectEvents(EventSource es, Uri uri)
        {
            Assert.Equal(new CommentEvent("hello"), await es.ReadAnyEventAsync().WithTimeout());
            Assert.Equal(new MessageEvent("message", "value1", null, uri), await es.ReadAnyEventAsync().WithTimeout());
            Assert.Equal(new MessageEvent("event2", "ça\nqué", null, uri), await es.ReadAnyEventAsync().WithTimeout());
            Assert.Equal(new MessageEvent("message", MakeLongString(0, 500) + MakeLongString(500, 1000), null, uri),
                await es.ReadAnyEventAsync());
        }

        private static string MakeLongString(int startNum, int endNum)
        {
            // This is meant to verify that we're able to read event data from the stream
            // that doesn't all fit into our initial read buffer at once.
            var ret = new StringBuilder();
            for (var i = startNum; i < endNum; i++)
            {
                ret.Append(i).Append("!");
            }
            return ret.ToString();
        }

        [Fact]
        public async Task CanReceiveUtf8EventDataAsBytes()
        {
            await WithMockConnectEventSource(
                mock => mock.ConfigureRequests(
                    MockConnectStrategy.RespondWithDataAndStayOpen(streamChunks)
                    ),
                async (mock, es) =>
                {
                    await es.StartAsync().WithTimeout();
                    await ExpectEvents(es, MockConnectStrategy.MockOrigin);
                });
        }
    }
}
