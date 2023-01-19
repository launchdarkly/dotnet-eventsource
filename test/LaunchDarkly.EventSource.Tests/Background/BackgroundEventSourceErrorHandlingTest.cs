using System;
using System.Threading.Tasks;
using LaunchDarkly.EventSource.Events;
using LaunchDarkly.EventSource.Exceptions;
using LaunchDarkly.Logging;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.EventSource.Background
{
	public class BackgroundEventSourceErrorHandlingTest : BaseTest
    {
        private readonly MockConnectStrategy _mockConnect = new MockConnectStrategy();

        public BackgroundEventSourceErrorHandlingTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public void ErrorFromOnOpenIsCaughtAndLoggedAndRedispatched()
        {
            var fakeError = new Exception("sorry");
            _mockConnect.ConfigureRequests(MockConnectStrategy.RespondWithStream());
            var sink = new SimpleEventSink();

            using (var bes = new BackgroundEventSource(BaseBuilder().Build()))
            {
                sink.Listen(bes);
                bes.Opened += (sender, args) => throw fakeError;

                Task.Run(bes.RunAsync);

                Assert.Equal(new StartedEvent(), sink.Take());
                Assert.Equal(new FaultEvent(fakeError), sink.Take());
                VerifyErrorLogged(fakeError);
            }
        }

        [Fact]
        public void ErrorFromOnMessageIsCaughtAndLoggedAndRedispatched()
        {
            var fakeError = new Exception("sorry");
            _mockConnect.ConfigureRequests(
                MockConnectStrategy.RespondWithDataAndStayOpen("data: data1\n\n")
                );
            var sink = new SimpleEventSink();

            using (var bes = new BackgroundEventSource(BaseBuilder().Build()))
            {
                sink.Listen(bes);
                bes.MessageReceived += (sender, args) => throw fakeError;

                Task.Run(bes.RunAsync);

                Assert.Equal(new StartedEvent(), sink.Take());
                Assert.Equal(new MessageEvent("message", "data1", _mockConnect.Origin), sink.Take());
                Assert.Equal(new FaultEvent(fakeError), sink.Take());
                VerifyErrorLogged(fakeError);
            }
        }

        [Fact]
        public void ErrorFromOnCommentIsCaughtAndLoggedAndRedispatched()
        {
            var fakeError = new Exception("sorry");
            _mockConnect.ConfigureRequests(
                MockConnectStrategy.RespondWithDataAndStayOpen(":hello\n")
                );
            var sink = new SimpleEventSink();

            using (var bes = new BackgroundEventSource(BaseBuilder().Build()))
            {
                sink.Listen(bes);
                bes.CommentReceived += (sender, args) => throw fakeError;

                Task.Run(bes.RunAsync);

                Assert.Equal(new StartedEvent(), sink.Take());
                Assert.Equal(new CommentEvent("hello"), sink.Take());
                Assert.Equal(new FaultEvent(fakeError), sink.Take());
                VerifyErrorLogged(fakeError);
            }
        }

        [Fact]
        public void ErrorFromOnCloseIsCaughtAndLoggedAndRedispatched()
        {
            var fakeError = new Exception("sorry");
            _mockConnect.ConfigureRequests(
                MockConnectStrategy.RespondWithDataAndThenEnd(":hello\n"),
                MockConnectStrategy.RespondWithStream()
                );
            var sink = new SimpleEventSink();

            using (var bes = new BackgroundEventSource(BaseBuilder().Build()))
            {
                sink.Listen(bes);
                bes.Closed += (sender, args) => throw fakeError;

                Task.Run(bes.RunAsync);

                Assert.Equal(new StartedEvent(), sink.Take());
                Assert.Equal(new CommentEvent("hello"), sink.Take());
                Assert.Equal(new FaultEvent(new StreamClosedByServerException()), sink.Take());
                Assert.Equal(new SimpleEventSink.ClosedEvent(ReadyState.Closed), sink.Take());
                Assert.Equal(new FaultEvent(fakeError), sink.Take());
                VerifyErrorLogged(fakeError);
            }
        }

        [Fact]
        public void ErrorFromOnErrorIsCaughtAndLogged()
        {
            var fakeError1 = new Exception("sorry");
            var fakeError2 = new Exception("not sorry");
            _mockConnect.ConfigureRequests(
                MockConnectStrategy.RespondWithDataAndThenEnd(":hello\n")
                );
            var sink = new SimpleEventSink();

            using (var bes = new BackgroundEventSource(BaseBuilder().Build()))
            {
                sink.Listen(bes);
                bes.Opened += (sender, args) => throw fakeError1;
                bes.Error += (sender, args) => throw fakeError2;

                Task.Run(bes.RunAsync);

                Assert.Equal(new StartedEvent(), sink.Take());
                Assert.Equal(new FaultEvent(fakeError1), sink.Take());

                // read the comment just for the sake of synchronization - otherwise
                // we couldn't be sure that it had finished dispatching the second error
                Assert.Equal(new CommentEvent("hello"), sink.Take());

                VerifyErrorLogged(fakeError2);
            }
        }

        private ConfigurationBuilder BaseBuilder() =>
            new ConfigurationBuilder(_mockConnect)
                .ErrorStrategy(ErrorStrategy.AlwaysContinue)
                .InitialRetryDelay(TimeSpan.FromMilliseconds(1))
                .Logger(_testLogger);

        private void VerifyErrorLogged(Exception ex)
        {
            var expectedPattern = "BackgroundEventSource caught an exception.*" +
                LogValues.ExceptionSummary(ex);
            Assert.True(
                _logCapture.HasMessageWithRegex(
                    LogLevel.Error,
                    expectedPattern
                ), "did not see expected log message (" + expectedPattern + ")");
        }
    }
}

