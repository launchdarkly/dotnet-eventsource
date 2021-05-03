using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LaunchDarkly.TestHelpers.HttpTest;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.EventSource.Tests.TestHelpers;

namespace LaunchDarkly.EventSource.Tests
{
    public abstract class EventSourceStreamReadingTestBase : BaseTest
    {
        // There are two subclasses of this test class because the stream reading logic has two
        // code paths, one for reading raw UTF-8 byte data and one for everything else, so we want
        // to make sure we have the same test coverage in both cases.

        protected EventSourceStreamReadingTestBase(ITestOutputHelper testOutput) : base(testOutput) { }

        protected abstract bool IsRawUtf8Mode { get; }

        protected override void AddBaseConfig(ConfigurationBuilder builder)
        {
            builder.PreferDataAsUtf8Bytes(IsRawUtf8Mode);
        }

        protected void WithServerAndStartedEventSource(Handler handler, Action<EventSource, EventSink> action) =>
            WithServerAndStartedEventSource(handler, null, action);

        protected void WithServerAndStartedEventSource(Handler handler, Action<ConfigurationBuilder> modConfig, Action<EventSource, EventSink> action)
        {
            using (var server = HttpServer.Start(handler))
            {
                using (var es = MakeEventSource(server.Uri, modConfig))
                {
                    var eventSink = new EventSink(es, _testLogging) { ExpectUtf8Data = IsRawUtf8Mode };
                    _ = Task.Run(es.StartAsync);
                    action(es, eventSink);
                }
            }
        }

        [Fact]
        public void ReceiveComment()
        {
            var commentSent = ": hello";

            var handler = StartStream().Then(Handlers.WriteChunkString(commentSent + "\n"))
                .Then(LeaveStreamOpen());

            WithServerAndStartedEventSource(handler, (_, eventSink) =>
            {
                eventSink.ExpectActions(
                        EventSink.OpenedAction(),
                        EventSink.CommentReceivedAction(commentSent)
                        );
            });
        }

        [Fact]
        public void ReceiveEventWithOnlyData()
        {
            var eventData = "this is a test message";
            var sse = "data: " + eventData + "\n\n";

            var handler = StartStream().Then(Handlers.WriteChunkString(sse))
                .Then(LeaveStreamOpen());

            WithServerAndStartedEventSource(handler, (_, eventSink) =>
            {
                eventSink.ExpectActions(
                    EventSink.OpenedAction(),
                    EventSink.MessageReceivedAction(new MessageEvent(MessageEvent.DefaultName, eventData, _uri))
                    );
            });
        }

        [Fact]
        public void ReceiveEventWithEventNameAndData()
        {
            var eventName = "test event";
            var eventData = "this is a test message";
            var sse = "event: " + eventName + "\ndata: " + eventData + "\n\n";

            var handler = StartStream().Then(Handlers.WriteChunkString(sse))
                .Then(LeaveStreamOpen());

            WithServerAndStartedEventSource(handler, (_, eventSink) =>
            {
                eventSink.ExpectActions(
                    EventSink.OpenedAction(),
                    EventSink.MessageReceivedAction(new MessageEvent(eventName, eventData, _uri))
                    );
            });
        }

        [Fact]
        public void ReceiveEventWithID()
        {
            var eventName = "test event";
            var eventData = "this is a test message";
            var eventId = "123abc";
            var sse = "event: " + eventName + "\ndata: " + eventData + "\nid: " + eventId + "\n\n";

            var handler = StartStream().Then(Handlers.WriteChunkString(sse))
                .Then(LeaveStreamOpen());

            WithServerAndStartedEventSource(handler, (_, eventSink) =>
            {
                eventSink.ExpectActions(
                    EventSink.OpenedAction(),
                    EventSink.MessageReceivedAction(new MessageEvent(eventName, eventData, eventId, _uri))
                    );
            });
        }

        [Fact]
        public void ReceiveEventStreamInChunks()
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

            var handler = StartStream().Then(async ctx =>
            {
                foreach (var s in chunks)
                {
                    await Handlers.WriteChunkString(s)(ctx);
                }
            }).Then(LeaveStreamOpen());

            var expectedActions = new List<EventSink.Action>();
            expectedActions.Add(EventSink.OpenedAction());
            foreach (var data in eventData)
            {
                expectedActions.Add(EventSink.MessageReceivedAction(new MessageEvent(MessageEvent.DefaultName, data, _uri)));
            }

            WithServerAndStartedEventSource(handler, (_, eventSink) =>
            {
                eventSink.ExpectActions(expectedActions.ToArray());
            });
        }

        [Fact]
        public void DetectReadTimeout()
        {
            TimeSpan readTimeout = TimeSpan.FromMilliseconds(300);
            TimeSpan timeToWait = readTimeout + readTimeout;

            var handler = StartStream()
                .Then(Handlers.WriteChunkString(":comment1\n"))
                .Then(Handlers.WriteChunkString(":comment2\n"))
                .Then(Handlers.Delay(timeToWait))
                .Then(Handlers.WriteChunkString(":comment3\n"));

            WithServerAndStartedEventSource(handler, config => config.ReadTimeout(readTimeout), (_, eventSink) =>
            {
                eventSink.ExpectActions(
                    EventSink.OpenedAction(),
                    EventSink.CommentReceivedAction(":comment1"),
                    EventSink.CommentReceivedAction(":comment2"),
                    EventSink.ErrorAction(new ReadTimeoutException()),
                    EventSink.ClosedAction()
                    );
            });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CanRestartStream(bool resetBackoff)
        {
            // This test is in EventSourceStreamReadingTest rather than EventSourceReconnectingTest
            // because the important thing here is that the stream reading logic can be interrupted.
            int nAttempts = 3;
            var initialDelay = TimeSpan.FromMilliseconds(50);

            var anEvent = new MessageEvent("put", "x", _uri);
            var handler = Handlers.Sequential(
                Enumerable.Range(0, nAttempts + 1).Select(_ =>
                    StartStream().Then(WriteEvent(anEvent)).Then(LeaveStreamOpen())
                ).ToArray());

            var backoffs = new List<TimeSpan>();

            using (var server = HttpServer.Start(handler))
            {
                using (var es = MakeEventSource(server.Uri, config => config.InitialRetryDelay(initialDelay)))
                {
                    var sink = new EventSink(es, _testLogging);
                    es.Closed += (_, ex) =>
                    {
                        backoffs.Add(es.BackOffDelay);
                    };
                    _ = Task.Run(es.StartAsync);

                    sink.ExpectActions(
                        EventSink.OpenedAction(),
                        EventSink.MessageReceivedAction(anEvent)
                        );

                    for (var i = 0; i < nAttempts; i++)
                    {
                        es.Restart(resetBackoff);

                        sink.ExpectActions(
                            EventSink.ClosedAction(),
                            EventSink.OpenedAction(),
                            EventSink.MessageReceivedAction(anEvent)
                            );
                    }
                }
            }

            if (resetBackoff)
            {
                Assert.All(backoffs, delay => Assert.InRange(delay, TimeSpan.Zero, initialDelay));
            }
            else
            {
                AssertBackoffsAlwaysIncrease(backoffs, nAttempts);
            }
        }
    }

    public class EventSourceStreamReadingDefaultModeTest : EventSourceStreamReadingTestBase
    {
        protected override bool IsRawUtf8Mode => false;

        public EventSourceStreamReadingDefaultModeTest(ITestOutputHelper testOutput) : base(testOutput) { }
    }

    public class EventSourceStreamReadingRawUtf8ModeTest : EventSourceStreamReadingTestBase
    {
        protected override bool IsRawUtf8Mode => true;

        public EventSourceStreamReadingRawUtf8ModeTest(ITestOutputHelper testOutput) : base(testOutput) { }
    }
}
