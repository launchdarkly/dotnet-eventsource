using System.Linq;
using System.Threading.Tasks;
using LaunchDarkly.EventSource.Events;
using LaunchDarkly.Logging;
using LaunchDarkly.TestHelpers.HttpTest;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.EventSource.TestHelpers;

namespace LaunchDarkly.EventSource
{
    public class EventSourceLoggingTest : BaseTest
    {
        public EventSourceLoggingTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public async Task UsesDefaultLoggerNameWhenLogAdapterIsSpecified()
        {
            await WithMockConnectEventSource(
                mock => mock.ConfigureRequests(
                        MockConnectStrategy.RespondWithDataAndThenEnd("data:\n\n")
                    ),
                async (mock, es) =>
                {
                    await es.StartAsync();

                    Assert.NotEmpty(_logCapture.GetMessages());
                    Assert.True(_logCapture.GetMessages().All(m => m.LoggerName == Configuration.DefaultLoggerName),
                        _logCapture.ToString());
                });
        }

        [Fact]
        public async Task CanSpecifyLoggerInstance()
        {
            await WithMockConnectEventSource(
                mock => mock.ConfigureRequests(
                        MockConnectStrategy.RespondWithDataAndThenEnd("data:\n\n")
                    ),
                c => c.Logger(_logCapture.Logger("special")),
                async (mock, es) =>
                {
                    await es.StartAsync();

                    Assert.NotEmpty(_logCapture.GetMessages());
                    Assert.True(_logCapture.GetMessages().All(m => m.LoggerName == "special"), _logCapture.ToString());
                });
        }

        [Fact]
        public async Task ConnectingLogMessage()
        {
            // This one is specific to HttpConnectStrategy so we must use real HTTP
            var handler = StartStream().Then(WriteComment(""));
            await WithServerAndEventSource(handler, async (server, es) =>
            {
                await es.StartAsync();

                Assert.True(_logCapture.HasMessageWithText(LogLevel.Debug,
                    "Making GET request to EventSource URI " + server.Uri),
                    _logCapture.ToString());
            });
        }

        [Fact]
        public async Task EventReceivedLogMessage()
        {
            await WithMockConnectEventSource(
                mock => mock.ConfigureRequests(
                        MockConnectStrategy.RespondWithDataAndThenEnd("event:abc\ndata:\n\n")
                    ),
                async (mock, es) =>
                {
                    await es.StartAsync();

                    await es.ReadMessageAsync();

                    Assert.True(_logCapture.HasMessageWithText(LogLevel.Debug,
                        string.Format(@"Received event ""abc""")));
                });
        }
    }
}
