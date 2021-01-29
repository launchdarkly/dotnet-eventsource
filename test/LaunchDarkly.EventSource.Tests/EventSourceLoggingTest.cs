using System.Linq;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.EventSource.Tests
{
    public class EventSourceLoggingTest : BaseTest
    {
        private static readonly MessageEvent BasicEvent = new MessageEvent("thing", "test", _uri);

        private static StubMessageHandler HandlerWithBasicEvent() =>
            new StubMessageHandler(StubResponse.StartStream(StreamAction.Write(BasicEvent)));

        public EventSourceLoggingTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public void UsesDefaultLoggerNameWhenLogAdapterIsSpecified()
        {
            var config = new ConfigurationBuilder(_uri)
                .HttpMessageHandler(HandlerWithBasicEvent())
                .LogAdapter(_logCapture)
                .Build();

            using (var es = new EventSource(config))
            {
                var eventSink = new EventSink(es);
                _ = Task.Run(es.StartAsync);

                eventSink.ExpectActions(EventSink.OpenedAction());

                Assert.NotEmpty(_logCapture.GetMessages());
                Assert.True(_logCapture.GetMessages().All(m => m.LoggerName == Configuration.DefaultLoggerName),
                    _logCapture.ToString());
            }
        }

        [Fact]
        public void CanSpecifyLoggerInstance()
        {
            var config = new ConfigurationBuilder(_uri)
                .HttpMessageHandler(HandlerWithBasicEvent())
                .Logger(_logCapture.Logger("special"))
                .Build();

            using (var es = new EventSource(config))
            {
                var eventSink = new EventSink(es);
                _ = Task.Run(es.StartAsync);

                eventSink.ExpectActions(EventSink.OpenedAction());

                Assert.NotEmpty(_logCapture.GetMessages());
                Assert.True(_logCapture.GetMessages().All(m => m.LoggerName == "special"), _logCapture.ToString());
            }
        }


        [Fact]
        public void ConnectingLogMessage()
        {
            var config = new ConfigurationBuilder(_uri)
                .HttpMessageHandler(HandlerWithBasicEvent())
                .LogAdapter(_logCapture)
                .Build();

            using (var es = new EventSource(config))
            {
                var eventSink = new EventSink(es);
                _ = Task.Run(es.StartAsync);

                eventSink.ExpectActions(EventSink.OpenedAction());

                Assert.True(_logCapture.HasMessageWithText(LogLevel.Debug,
                    "Making GET request to EventSource URI " + _uri),
                    _logCapture.ToString());
            }
        }

        [Fact]
        public void EventReceivedLogMessage()
        {
            var config = new ConfigurationBuilder(_uri)
                .HttpMessageHandler(HandlerWithBasicEvent())
                .LogAdapter(_logCapture)
                .Build();

            using (var es = new EventSource(config))
            {
                var eventSink = new EventSink(es, _testLogging);
                _ = Task.Run(es.StartAsync);

                eventSink.ExpectActions(
                    EventSink.OpenedAction(),
                    EventSink.MessageReceivedAction(BasicEvent)
                    );

                Assert.True(_logCapture.HasMessageWithText(LogLevel.Debug,
                    string.Format(@"Received event ""{0}""", BasicEvent.Name)));
            }
        }
    }
}
