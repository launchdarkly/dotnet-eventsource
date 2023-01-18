using System;
using System.Net.Http;
using System.Threading.Tasks;
using LaunchDarkly.EventSource.Events;
using LaunchDarkly.EventSource.Internal;
using LaunchDarkly.TestHelpers.HttpTest;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.EventSource.TestHelpers;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// Tests of basic EventSource behavior using real HTTP requests.
    /// </summary>
    public class HttpConnectStrategyWithEventSource : BaseTest
    {
        public HttpConnectStrategyWithEventSource(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public async Task CustomHttpClientIsNotClosedWhenEventSourceCloses()
        {
            using (var server = HttpServer.Start(Handlers.Status(200)))
            {
                using (var client = new HttpClient())
                {
                    var es = new EventSource(
                        Configuration.Builder(
                            ConnectStrategy.Http(server.Uri).HttpClient(client)
                            ).Build());
                    es.Close();

                    await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, server.Uri));
                }
            }
        }

        [Fact]
        public async Task LastEventIdHeaderIsNotSetByDefault()
        {
            await WithServerAndEventSource(StreamWithCommentThatStaysOpen,
                async (server, es) =>
                {
                    await es.StartAsync();
                    var req = server.Recorder.RequireRequest();
                    Assert.Null(req.Headers.Get(Constants.LastEventIdHttpHeader));
                });
        }

        [Fact]
        public async Task LastEventIdHeaderIsSetIfConfigured()
        {
            var lastEventId = "abc123";

            await WithServerAndEventSource(StreamWithCommentThatStaysOpen,
                null,
                config => config.LastEventId(lastEventId),
                async (server, es) =>
            {
                await es.StartAsync();

                var req = server.Recorder.RequireRequest();
                Assert.Equal(lastEventId, req.Headers.Get(Constants.LastEventIdHttpHeader));
            });
        }

        [Fact]
        public async Task ReadTimeoutIsDetected()
        {
            TimeSpan readTimeout = TimeSpan.FromMilliseconds(200);
            var streamHandler = StartStream()
                .Then(Handlers.WriteChunkString("data: event1\n\ndata: e"))
                .Then(Handlers.Delay(readTimeout + readTimeout))
                .Then(Handlers.WriteChunkString("vent2\n\n"));
            await WithServerAndEventSource(streamHandler,
                http => http.ReadTimeout(readTimeout),
                null,
                async (server, es) =>
                {
                    await es.StartAsync();
                    Assert.Equal(new MessageEvent(MessageEvent.DefaultName, "event1", server.Uri),
                        await es.ReadMessageAsync());
                    await Assert.ThrowsAnyAsync<ReadTimeoutException>(() => es.ReadMessageAsync());
                });
        }
    }
}
