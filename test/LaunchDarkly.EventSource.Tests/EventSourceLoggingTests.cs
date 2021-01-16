using System;
using System.Linq;
using System.Threading; 
using LaunchDarkly.Logging;
using Xunit;

namespace LaunchDarkly.EventSource.Tests
{
    public class EventSourceLoggingTests
    {
        private readonly Uri _uri = new Uri("http://test.com");

        [Fact]
        public void UsesDefaultLoggerNameWhenLogAdapterIsSpecified()
        {
            var logCapture = Logs.Capture();

            var config = new ConfigurationBuilder(_uri)
                .HttpMessageHandler(HandlerWithBasicEvent())
                .LogAdapter(logCapture)
                .Build();

            using (var es = new EventSource(config))
            {
                var opened = new EventWaitHandle(false, EventResetMode.ManualReset);
                es.Opened += (sender, e) => opened.Set();

                _ = es.StartAsync();
                opened.WaitOne();

                Assert.NotEmpty(logCapture.GetMessages());
                Assert.True(logCapture.GetMessages().All(m => m.LoggerName == Configuration.DefaultLoggerName));
            }
        }

        [Fact]
        public void CanSpecifyLoggerInstance()
        {
            var logCapture = Logs.Capture();
            var logger = logCapture.Logger("special");

            var config = new ConfigurationBuilder(_uri)
                .HttpMessageHandler(HandlerWithBasicEvent())
                .Logger(logger)
                .Build();

            using (var es = new EventSource(config))
            {
                var opened = new EventWaitHandle(false, EventResetMode.ManualReset);
                es.Opened += (sender, e) => opened.Set();

                _ = es.StartAsync();
                opened.WaitOne();

                Assert.NotEmpty(logCapture.GetMessages());
                Assert.True(logCapture.GetMessages().All(m => m.LoggerName == "special"));
            }
        }


        [Fact]
        public void ConnectingLogMessage()
        {
            var logCapture = Logs.Capture();

            var config = new ConfigurationBuilder(_uri)
                .HttpMessageHandler(HandlerWithBasicEvent())
                .LogAdapter(logCapture)
                .Build();

            using (var es = new EventSource(config))
            {
                var opened = new EventWaitHandle(false, EventResetMode.ManualReset);
                es.Opened += (sender, e) => opened.Set();

                _ = es.StartAsync();
                opened.WaitOne();

                Assert.True(logCapture.HasMessageWithText(LogLevel.Debug,
                    "Making GET request to EventSource URI " + _uri),
                    logCapture.ToString());
            }
        }

        [Fact]
        public void EventReceivedLogMessage()
        {
            var logCapture = Logs.Capture();

            var config = new ConfigurationBuilder(_uri)
                .HttpMessageHandler(HandlerWithBasicEvent())
                .LogAdapter(logCapture)
                .Build();

            using (var es = new EventSource(config))
            {
                var received = new EventWaitHandle(false, EventResetMode.ManualReset);
                es.MessageReceived += (sender, e) => received.Set();

                _ = es.StartAsync();
                received.WaitOne();

                Assert.True(logCapture.HasMessageWithText(LogLevel.Debug,
                    "Received event \"thing\""),
                    logCapture.ToString());
            }
        }

        private StubMessageHandler HandlerWithBasicEvent()
        {
            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream(StreamAction.Write("event: thing\ndata: test\n\n")));
            return handler;
        }
    }
}
