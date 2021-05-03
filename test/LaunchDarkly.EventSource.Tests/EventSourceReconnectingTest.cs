using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using LaunchDarkly.TestHelpers.HttpTest;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.EventSource.Tests.TestHelpers;

namespace LaunchDarkly.EventSource.Tests
{
    public class EventSourceReconnectingTest : BaseTest
    {
        public EventSourceReconnectingTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public void ReconnectAfterHttpError()
        {
            HttpStatusCode error1 = HttpStatusCode.BadRequest, error2 = HttpStatusCode.InternalServerError;
            var message = new MessageEvent("put", "hello", _uri);

            var handler = Handlers.Sequential(
                Handlers.Status((int)error1),
                Handlers.Status((int)error2),
                StartStream().Then(WriteEvent(message)).Then(LeaveStreamOpen())
                );

            WithServerAndEventSource(handler, c => c.InitialRetryDelay(TimeSpan.FromMilliseconds(20)), (server, es) =>
            {
                var eventSink = new EventSink(es, _testLogging);
                _ = Task.Run(es.StartAsync);

                var action1 = eventSink.ExpectAction();
                var ex1 = Assert.IsType<EventSourceServiceUnsuccessfulResponseException>(action1.Exception);
                Assert.Equal((int)error1, ex1.StatusCode);

                eventSink.ExpectActions(EventSink.ClosedAction());

                var action2 = eventSink.ExpectAction();
                var ex2 = Assert.IsType<EventSourceServiceUnsuccessfulResponseException>(action2.Exception);
                Assert.Equal((int)error2, ex2.StatusCode);

                eventSink.ExpectActions(
                    EventSink.ClosedAction(),
                    EventSink.OpenedAction(),
                    EventSink.MessageReceivedAction(message)
                    );
            });
        }

        [Fact]
        public void SendMostRecentEventIdOnReconnect()
        {
            var initialEventId = "abc123";
            var eventId1 = "xyz456";
            var message1 = new MessageEvent("put", "this is a test message", eventId1, _uri);

            var handler = Handlers.Sequential(
                StartStream().Then(WriteEvent(message1)),
                StartStream().Then(LeaveStreamOpen())
                );

            WithServerAndEventSource(handler, c => c.InitialRetryDelay(TimeSpan.FromMilliseconds(20)).LastEventId(initialEventId), (server, es) =>
            {
                var eventSink = new EventSink(es, _testLogging);
                _ = Task.Run(es.StartAsync);

                var req1 = server.Recorder.RequireRequest();
                Assert.Equal(initialEventId, req1.Headers["Last-Event-Id"]);

                var req2 = server.Recorder.RequireRequest();
                Assert.Equal(eventId1, req2.Headers["Last-Event-Id"]);
            });
        }

        [Fact]
        public void RetryDelayDurationsShouldIncrease()
        {
            var nAttempts = 3;
            var steps = new List<Handler>();
            steps.Add(StartStream());
            for (var i = 0; i < nAttempts; i++)
            {
                steps.Add(Handlers.WriteChunkString(":hi\n"));
            }
            steps.Add(LeaveStreamOpen());
            var handler = Handlers.Sequential(steps.ToArray());

            var backoffs = new List<TimeSpan>();

            WithServerAndEventSource(handler, c => c.InitialRetryDelay(TimeSpan.FromMilliseconds(100)), (server, es) =>
            {
                _ = new EventSink(es, _testLogging);
                es.Closed += (_, state) =>
                {
                    backoffs.Add(es.BackOffDelay);
                };

                _ = Task.Run(es.StartAsync);

                for (int i = 0; i <= nAttempts; i++)
                {
                    _ = server.Recorder.RequireRequest();
                }
            });

            AssertBackoffsAlwaysIncrease(backoffs, nAttempts);
        }

        [Fact]
        public async Task NoReconnectAttemptIsMadeIfErrorHandlerClosesEventSource()
        {
            var handler = Handlers.Sequential(
                Handlers.Status((int)HttpStatusCode.Unauthorized),
                StartStream().Then(LeaveStreamOpen())
                );

            using (var server = HttpServer.Start(handler))
            {
                using (var es = MakeEventSource(server.Uri))
                {
                    es.Error += (_, e) => es.Close();

                    await es.StartAsync();

                    server.Recorder.RequireRequest();
                    server.Recorder.RequireNoRequests(TimeSpan.FromMilliseconds(100));
                }
            }
        }
    }
}
