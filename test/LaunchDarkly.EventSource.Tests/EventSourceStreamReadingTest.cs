using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LaunchDarkly.EventSource.Events;
using LaunchDarkly.EventSource.Exceptions;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.EventSource.TestHelpers;

namespace LaunchDarkly.EventSource
{
    public class EventSourceStreamReadingTest : BaseTest
    {
        public EventSourceStreamReadingTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public async Task ReceiveComment()
        {
            await WithMockConnectEventSource(
                mock => mock.ConfigureRequests(
                    MockConnectStrategy.RespondWithDataAndStayOpen(":hello\n")
                    ),
                async (mock, es) =>
                {
                    await es.StartAsync().WithTimeout();
                    var e = await es.ReadAnyEventAsync().WithTimeout();
                    Assert.Equal(new CommentEvent("hello"), e);
                });
        }

        [Fact]
        public async Task ReceiveEventWithOnlyData()
        {
            var eventData = "this is a test message";
            var streamData = "data: " + eventData + "\n\n";

            await WithMockConnectEventSource(
                mock => mock.ConfigureRequests(
                    MockConnectStrategy.RespondWithDataAndStayOpen(streamData)
                    ),
                async (mock, es) =>
                {
                    await es.StartAsync().WithTimeout();
                    var e = await es.ReadAnyEventAsync().WithTimeout();
                    var m = Assert.IsType<MessageEvent>(e);
                    Assert.Equal(MessageEvent.DefaultName, m.Name);
                    Assert.Equal(eventData, m.Data);
                    Assert.Equal(MockConnectStrategy.MockOrigin, m.Origin);
                    Assert.Null(m.LastEventId);
                });
        }

        [Fact]
        public async Task ReceiveEventWithEventNameAndData()
        {
            var eventName = "test event";
            var eventData = "this is a test message";
            var streamData = "event: " + eventName + "\ndata: " + eventData + "\n\n";

            await WithMockConnectEventSource(
                mock => mock.ConfigureRequests(
                    MockConnectStrategy.RespondWithDataAndStayOpen(streamData)
                    ),
                async (mock, es) =>
                {
                    await es.StartAsync().WithTimeout();
                    var e = await es.ReadAnyEventAsync().WithTimeout();
                    var m = Assert.IsType<MessageEvent>(e);
                    Assert.Equal(eventName, m.Name);
                    Assert.Equal(eventData, m.Data);
                    Assert.Equal(MockConnectStrategy.MockOrigin, m.Origin);
                    Assert.Null(m.LastEventId);
                });
        }

        [Fact]
        public async Task ReceiveEventWithID()
        {
            var eventName = "test event";
            var eventData = "this is a test message";
            var eventId = "123abc";
            var streamData = "event: " + eventName + "\ndata: " + eventData + "\nid: " + eventId + "\n\n";

            await WithMockConnectEventSource(
                mock => mock.ConfigureRequests(
                    MockConnectStrategy.RespondWithDataAndStayOpen(streamData)
                    ),
                async (mock, es) =>
                {
                    await es.StartAsync().WithTimeout();
                    var e = await es.ReadAnyEventAsync().WithTimeout();
                    var m = Assert.IsType<MessageEvent>(e);
                    Assert.Equal(eventName, m.Name);
                    Assert.Equal(eventData, m.Data);
                    Assert.Equal(MockConnectStrategy.MockOrigin, m.Origin);
                    Assert.Equal(eventId, m.LastEventId);
                });
        }

        [Fact]
        public async Task ReceiveEventStreamInChunks()
        {
            // This simply verifies that chunked streaming works as expected and that events are being
            // parsed correctly regardless of how the chunks line up with the events.

            var eventData = new List<string>();
            var chunks = new List<string>();
            for (var i = 0; i < 200; i++)
            {
                eventData.Add(string.Format("data{0}", i) + new string('x', i % 7));
            }
            var allBody = string.Concat(eventData.Select(data => "data:" + data + "\n\n"));
            for (var pos = 0; ;)
            {
                int i = chunks.Count;
                int chunkSize = i % 20 + 1;
                if (pos + chunkSize >= allBody.Length)
                {
                    chunks.Add(allBody.Substring(pos));
                    break;
                }
                chunks.Add(allBody.Substring(pos, chunkSize));
                pos += chunkSize;
            }

            await WithMockConnectEventSource(
                mock => mock.ConfigureRequests(
                    MockConnectStrategy.RespondWithDataAndStayOpen(chunks.ToArray())
                    ),
                async (mock, es) =>
                {
                    await es.StartAsync().WithTimeout();
                    foreach (var data in eventData)
                    {
                        var e = await es.ReadAnyEventAsync().WithTimeout();
                        var m = Assert.IsType<MessageEvent>(e);
                        Assert.Equal(MessageEvent.DefaultName, m.Name);
                        Assert.Equal(data, m.Data);
                        Assert.Equal(MockConnectStrategy.MockOrigin, m.Origin);
                    }
                });
        }

        [Fact]
        public async Task CanRestartStream()
        {
            // This test is in EventSourceStreamReadingTest rather than EventSourceReconnectingTest
            // because the important thing here is that the stream reading logic can be interrupted.
            int nAttempts = 3;
            var initialDelay = TimeSpan.FromMilliseconds(50);

            var anEvent = new MessageEvent("put", "x", MockConnectStrategy.MockOrigin);
            var handlers =
                Enumerable.Range(0, nAttempts + 1).Select(_ =>
                    MockConnectStrategy.RespondWithDataAndStayOpen("event:put\ndata:x\n\n")
                ).ToArray();

            var backoffs = new List<TimeSpan>();

            await WithMockConnectEventSource(
                mock => mock.ConfigureRequests(handlers),
                c => c.InitialRetryDelay(initialDelay).ErrorStrategy(ErrorStrategy.AlwaysContinue),
                async (mock, es) =>
                {
                    await es.StartAsync().WithTimeout();

                    Assert.Equal(anEvent, await es.ReadAnyEventAsync().WithTimeout());

                    for (var i = 0; i < nAttempts; i++)
                    {
                        es.Interrupt();

                        Assert.Equal(new FaultEvent(new StreamClosedByCallerException()),
                            await es.ReadAnyEventAsync().WithTimeout());
                        Assert.Equal(new StartedEvent(), await es.ReadAnyEventAsync().WithTimeout());
                        Assert.Equal(anEvent, await es.ReadAnyEventAsync().WithTimeout());
                    }
                });
        }
    }
}
