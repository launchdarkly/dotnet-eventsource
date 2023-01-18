using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using LaunchDarkly.EventSource.Events;
using LaunchDarkly.EventSource.Exceptions;
using LaunchDarkly.TestHelpers.HttpTest;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.EventSource.TestHelpers;

namespace LaunchDarkly.EventSource
{
    public class EventSourceReconnectingTest : BaseTest
    {
        public EventSourceReconnectingTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public async Task SendMostRecentEventIdOnReconnect()
        {
            var initialEventId = "abc123";
            var eventId1 = "xyz456";
            var message1 = new MessageEvent("put", "hello", eventId1, _uri);

            await WithMockConnectEventSource(
                mock => mock.ConfigureRequests(
                    MockConnectStrategy.RespondWithDataAndThenEnd(message1.AsSSEData()),
                    MockConnectStrategy.RespondWithStream()
                    ),
                c => c.LastEventId(initialEventId)
                    .ErrorStrategy(ErrorStrategy.AlwaysContinue),
                async (mock, es) =>
                {
                    await es.StartAsync().WithTimeout();

                    Assert.Equal(new MessageEvent("put", "hello", eventId1, mock.Origin),
                        await es.ReadAnyEventAsync().WithTimeout());

                    var p1 = mock.ReceivedConnections.Take();
                    Assert.Equal(initialEventId, p1.LastEventId);

                    Assert.Equal(new FaultEvent(new StreamClosedByServerException()),
                        await es.ReadAnyEventAsync().WithTimeout());

                    Assert.Equal(new StartedEvent(),
                        await es.ReadAnyEventAsync().WithTimeout());

                    var p2 = mock.ReceivedConnections.Take();
                    Assert.Equal(eventId1, p2.LastEventId);
                });
        }

        [Fact]
        public async Task RetryDelayStrategyIsAppliedEachTime()
        {
            var baseDelay = TimeSpan.FromMilliseconds(10);
            var increment = TimeSpan.FromMilliseconds(3);
            var nAttempts = 3;
            var requestHandlers = new List<MockConnectStrategy.RequestHandler>();
            for (var i = 0; i < nAttempts; i++)
            {
                requestHandlers.Add(MockConnectStrategy.RespondWithDataAndThenEnd(":hi\n"));
            }
            requestHandlers.Add(MockConnectStrategy.RespondWithDataAndStayOpen(":abc\n"));

            var expectedDelay = baseDelay;

            await WithMockConnectEventSource(
                mock => mock.ConfigureRequests(requestHandlers.ToArray()),
                c => c.ErrorStrategy(ErrorStrategy.AlwaysContinue)
                    .InitialRetryDelay(baseDelay)
                    .RetryDelayStrategy(new ArithmeticallyIncreasingDelayStrategy(increment, TimeSpan.Zero)),
                async (mock, es) =>
                {
                    await es.StartAsync().WithTimeout();

                    for (int i = 0; i < nAttempts; i++)
                    {
                        Assert.Equal(new CommentEvent("hi"),
                            await es.ReadAnyEventAsync().WithTimeout());
                        Assert.Equal(new FaultEvent(new StreamClosedByServerException()),
                            await es.ReadAnyEventAsync().WithTimeout());

                        Assert.Equal(expectedDelay, es.NextRetryDelay);
                        expectedDelay += increment;

                        Assert.Equal(new StartedEvent(),
                            await es.ReadAnyEventAsync().WithTimeout());
                    }
                });
        }

        public class ArithmeticallyIncreasingDelayStrategy : RetryDelayStrategy
        {
            private readonly TimeSpan _increment, _cumulative;

            public ArithmeticallyIncreasingDelayStrategy(TimeSpan increment, TimeSpan cumulative)
            {
                _increment = increment;
                _cumulative = cumulative;
            }

            public override Result Apply(TimeSpan baseRetryDelay) =>
                new Result
                {
                    Delay = baseRetryDelay + _cumulative,
                    Next = new ArithmeticallyIncreasingDelayStrategy(_increment,
                        _cumulative + _increment)
                };
        }
    }
}
