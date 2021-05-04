using System.Linq;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using LaunchDarkly.TestHelpers.HttpTest;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.EventSource.Tests.TestHelpers;

namespace LaunchDarkly.EventSource.Tests
{
    public class EventSourceLoggingTest : BaseTest
    {
        private static readonly MessageEvent BasicEvent = new MessageEvent("thing", "test", _uri);

        private static Handler HandlerWithBasicEvent() =>
            StartStream().Then(WriteEvent(BasicEvent)).Then(LeaveStreamOpen());

        public EventSourceLoggingTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public void UsesDefaultLoggerNameWhenLogAdapterIsSpecified()
        {
            WithServerAndEventSource(HandlerWithBasicEvent(), (server, es) =>
            {
                var eventSink = new EventSink(es);
                _ = Task.Run(es.StartAsync);

                eventSink.ExpectActions(EventSink.OpenedAction());

                Assert.NotEmpty(_logCapture.GetMessages());
                Assert.True(_logCapture.GetMessages().All(m => m.LoggerName == Configuration.DefaultLoggerName),
                    _logCapture.ToString());
            });
        }

        [Fact]
        public void CanSpecifyLoggerInstance()
        {
            WithServerAndEventSource(HandlerWithBasicEvent(), c => c.Logger(_logCapture.Logger("special")), (server, es) =>
            {
                var eventSink = new EventSink(es);
                _ = Task.Run(es.StartAsync);

                eventSink.ExpectActions(EventSink.OpenedAction());

                Assert.NotEmpty(_logCapture.GetMessages());
                Assert.True(_logCapture.GetMessages().All(m => m.LoggerName == "special"), _logCapture.ToString());
            });
        }

        [Fact]
        public void ConnectingLogMessage()
        {
            WithServerAndEventSource(HandlerWithBasicEvent(), (server, es) =>
            {
                var eventSink = new EventSink(es);
                _ = Task.Run(es.StartAsync);

                eventSink.ExpectActions(EventSink.OpenedAction());

                Assert.True(_logCapture.HasMessageWithText(LogLevel.Debug,
                    "Making GET request to EventSource URI " + server.Uri),
                    _logCapture.ToString());
            });
        }

        [Fact]
        public void EventReceivedLogMessage()
        {
            WithServerAndEventSource(HandlerWithBasicEvent(), (server, es) =>
            {
                var eventSink = new EventSink(es, _testLogging);
                _ = Task.Run(es.StartAsync);

                eventSink.ExpectActions(
                    EventSink.OpenedAction(),
                    EventSink.MessageReceivedAction(BasicEvent)
                    );

                Assert.True(_logCapture.HasMessageWithText(LogLevel.Debug,
                    string.Format(@"Received event ""{0}""", BasicEvent.Name)));
            });
        }
    }
}
