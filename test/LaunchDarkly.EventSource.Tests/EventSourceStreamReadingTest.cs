using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
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

        protected EventSource StartEventSource(HttpMessageHandler handler, out EventSink sink,
            Action<ConfigurationBuilder> modConfig = null)
        {
            var es = MakeEventSource(handler, modConfig);
            sink = new EventSink(es, _testLogging) { ExpectUtf8Data = IsRawUtf8Mode };
            _ = Task.Run(es.StartAsync);
            return es;
        }

        [Fact]
        public void ReceiveComment()
        {
            var commentSent = ": hello";

            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream(StreamAction.Write(commentSent + "\n\n")));

            using (var es = StartEventSource(handler, out var eventSink))
            {
                eventSink.ExpectActions(
                    EventSink.OpenedAction(),
                    EventSink.CommentReceivedAction(commentSent)
                    ); ;
            }
        }

        [Fact]
        public void ReceiveEventWithOnlyData()
        {
            var eventData = "this is a test message";
            var sse = "data: " + eventData + "\n\n";

            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream(StreamAction.Write(sse)));

            using (var es = StartEventSource(handler, out var eventSink))
            {
                eventSink.ExpectActions(
                    EventSink.OpenedAction(),
                    EventSink.MessageReceivedAction(new MessageEvent(MessageEvent.DefaultName, eventData, _uri))
                    );
            }
        }

        [Fact]
        public void ReceiveEventWithEventNameAndData()
        {
            var eventName = "test event";
            var eventData = "this is a test message";
            var sse = "event: " + eventName + "\ndata: " + eventData + "\n\n";

            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream(StreamAction.Write(sse)));

            using (var es = StartEventSource(handler, out var eventSink))
            {
                eventSink.ExpectActions(
                    EventSink.OpenedAction(),
                    EventSink.MessageReceivedAction(new MessageEvent(eventName, eventData, _uri))
                    );
            }
        }

        [Fact]
        public void ReceiveEventWithID()
        {
            var eventName = "test event";
            var eventData = "this is a test message";
            var eventId = "123abc";
            var sse = "event: " + eventName + "\ndata: " + eventData + "\nid: " + eventId + "\n\n";

            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream(StreamAction.Write(sse)));

            using (var es = StartEventSource(handler, out var eventSink))
            {
                eventSink.ExpectActions(
                    EventSink.OpenedAction(),
                    EventSink.MessageReceivedAction(new MessageEvent(eventName, eventData, eventId, _uri))
                    );
            }
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

            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream(
                chunks.Select(StreamAction.Write).ToArray()));

            var expectedActions = new List<EventSink.Action>();
            expectedActions.Add(EventSink.OpenedAction());
            foreach (var data in eventData)
            {
                expectedActions.Add(EventSink.MessageReceivedAction(new MessageEvent(MessageEvent.DefaultName, data, _uri)));
            }

            using (var es = StartEventSource(handler, out var eventSink))
            { 
                eventSink.ExpectActions(expectedActions.ToArray());
            }
        }

        [Fact]
        public void DetectReadTimeout()
        {
            TimeSpan readTimeout = TimeSpan.FromMilliseconds(300);
            TimeSpan timeToWait = readTimeout + readTimeout;

            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream(
                StreamAction.Write(":comment1\n"),
                StreamAction.Write(":comment2\n"),
                StreamAction.Write(":comment3\n").AfterDelay(timeToWait))
            );
            handler.QueueResponse(StubResponse.StartStream());

            using (var es = StartEventSource(handler, out var eventSink, config => config.ReadTimeout(readTimeout)))
            {
                eventSink.ExpectActions(
                    EventSink.OpenedAction(),
                    EventSink.CommentReceivedAction(":comment1"),
                    EventSink.CommentReceivedAction(":comment2"),
                    EventSink.ErrorAction(new ReadTimeoutException()),
                    EventSink.ClosedAction()
                    );
            }
        }

        [Fact]
        public void TimeoutDoesNotCauseUnobservedException()
        {
            TimeSpan readTimeout = TimeSpan.FromMilliseconds(10);

            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream()); // stream will hang with no data

            UnobservedTaskExceptionEventArgs receivedUnobservedException = null;
            EventHandler<UnobservedTaskExceptionEventArgs> exceptionHandler = (object sender, UnobservedTaskExceptionEventArgs e) =>
            {
                e.SetObserved();
                receivedUnobservedException = e;
            };
            TaskScheduler.UnobservedTaskException += exceptionHandler;

            // Force finalizer to run so that any unobserved exceptions caused by lingering tasks from earlier
            // tests will trigger a failure right now, making it clearer that the problem isn't in this test.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Assert.Null(receivedUnobservedException);

            using (var es = StartEventSource(handler, out var eventSink, config => config.ReadTimeout(readTimeout)))
            {
                try
                {
                    eventSink.ExpectActions(
                        EventSink.OpenedAction(),
                        EventSink.ErrorAction(new ReadTimeoutException())
                        );

                    // Force finalizer to run again to trigger any unobserved exceptions we caused now.
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    Assert.Null(receivedUnobservedException);
                }
                finally
                {
                    TaskScheduler.UnobservedTaskException -= exceptionHandler;
                }
            }
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
            var stream = StubResponse.StartStream(StreamAction.Write(anEvent));
            var handler = new StubMessageHandler();
            for (var i = 0; i <= nAttempts; i++)
            {
                handler.QueueResponse(stream);
            }

            var backoffs = new List<TimeSpan>();

            using (var es = MakeEventSource(handler, builder => builder.InitialRetryDelay(initialDelay)))
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
