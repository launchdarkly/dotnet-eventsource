using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
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

            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.WithStatus(error1));
            handler.QueueResponse(StubResponse.WithStatus(error2));
            handler.QueueResponse(StubResponse.StartStream(StreamAction.Write(message)));

            using (var es = MakeEventSource(handler, builder => builder.InitialRetryDelay(TimeSpan.FromMilliseconds(20))))
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
            }
        }

        [Fact]
        public void SendMostRecentEventIdOnReconnect()
        {
            var initialEventId = "abc123";
            var eventId1 = "xyz456";
            var message1 = new MessageEvent("put", "this is a test message", eventId1, _uri);

            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream(StreamAction.Write(message1), StreamAction.CloseStream()));
            handler.QueueResponse(StubResponse.StartStream());

            using (var es = MakeEventSource(handler, builder => builder.InitialRetryDelay(TimeSpan.FromMilliseconds(20))
                .LastEventId(initialEventId)))
            {
                var eventSink = new EventSink(es, _testLogging);
                _ = Task.Run(es.StartAsync);

                var req1 = handler.AwaitRequest();
                Assert.Contains(initialEventId, req1.Headers.GetValues("Last-Event-Id"));

                var req2 = handler.AwaitRequest();
                Assert.Contains(eventId1, req2.Headers.GetValues("Last-Event-Id"));
            }
        }

        [Fact]
        public void RetryDelayDurationsShouldIncrease()
        {
            var handler = new StubMessageHandler();

            var nAttempts = 3;
            for (var i = 0; i < nAttempts; i++)
            {
                handler.QueueResponse(StubResponse.StartStream(
                    StreamAction.Write(":hi\n"),
                    StreamAction.CloseStreamAbnormally()));//StubResponse.WithIOError());
            }
            handler.QueueResponse(StubResponse.StartStream());

            var backoffs = new List<TimeSpan>();

            using (var es = MakeEventSource(handler, builder => builder.InitialRetryDelay(TimeSpan.FromMilliseconds(100))))
            {
                _ = new EventSink(es, _testLogging);
                es.Error += (_, ex) =>
                {
                    backoffs.Add(es.BackOffDelay);
                };

                _ = Task.Run(es.StartAsync);

                for (int i = 0; i <= nAttempts; i++)
                {
                    _ = handler.AwaitRequest();
                }
            }

            AssertBackoffsAlwaysIncrease(backoffs, nAttempts);
        }

        [Fact]
        public async void NoReconnectAttemptIsMadeIfErrorHandlerClosesEventSource()
        {
            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.WithStatus(HttpStatusCode.Unauthorized));
            handler.QueueResponse(StubResponse.StartStream());

            using (var es = MakeEventSource(handler))
            {
                es.Error += (_, e) => es.Close();
                await es.StartAsync();
            }

            Assert.Single(handler.GetRequests());
        }
    }
}
