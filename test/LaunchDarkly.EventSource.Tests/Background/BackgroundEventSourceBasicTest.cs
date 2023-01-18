using System;
using System.Threading.Tasks;
using LaunchDarkly.EventSource.Events;
using LaunchDarkly.EventSource.Exceptions;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.EventSource.Background
{
	public class BackgroundEventSourceBasicTest : BaseTest
	{
		private readonly MockConnectStrategy _mockConnect = new MockConnectStrategy();

		public BackgroundEventSourceBasicTest(ITestOutputHelper testOutput) : base(testOutput) { }

		[Fact]
		public void EventSourceCannotBeNull()
		{
			Assert.ThrowsAny<ArgumentNullException>(() => new BackgroundEventSource((EventSource)null));
		}

        [Fact]
        public void ConfigurationCannotBeNull()
        {
            Assert.ThrowsAny<ArgumentNullException>(() => new BackgroundEventSource((Configuration)null));
        }

        [Fact]
		public void EventsAreDispatchedToHandler()
		{
			_mockConnect.ConfigureRequests(
				MockConnectStrategy.RespondWithDataAndStayOpen(
                    "event: event1\ndata: data1\n\n",
					":hello\n"
				));
			var sink = new SimpleEventSink();

            using (var bes = new BackgroundEventSource(BaseBuilder().Build()))
			{
				sink.Listen(bes);
				Task.Run(bes.RunAsync);

                Assert.Equal(new StartedEvent(), sink.Take());
                Assert.Equal(new MessageEvent("event1", "data1", _mockConnect.Origin), sink.Take());
                Assert.Equal(new CommentEvent("hello"), sink.Take());
            }
		}

		[Fact]
		public void AlwaysRetriesConnectionByDefault()
		{
            _mockConnect.ConfigureRequests(
                MockConnectStrategy.RespondWithDataAndThenEnd("data: data1\n\n"),
                MockConnectStrategy.RespondWithDataAndStayOpen("data: data2\n\n")
                );
            var sink = new SimpleEventSink();

            using (var bes = new BackgroundEventSource(BaseBuilder().Build()))
            {
                sink.Listen(bes);
                Task.Run(bes.RunAsync);

                Assert.Equal(new StartedEvent(), sink.Take());
                Assert.Equal(new MessageEvent("message", "data1", _mockConnect.Origin), sink.Take());
				Assert.Equal(new FaultEvent(new StreamClosedByServerException()), sink.Take());
				Assert.Equal(new SimpleEventSink.ClosedEvent(ReadyState.Closed), sink.Take());
                Assert.Equal(new StartedEvent(), sink.Take());
                Assert.Equal(new MessageEvent("message", "data2", _mockConnect.Origin), sink.Take());
            }
        }

		private ConfigurationBuilder BaseBuilder() =>
			new ConfigurationBuilder(_mockConnect)
				.ErrorStrategy(ErrorStrategy.AlwaysContinue)
				.InitialRetryDelay(TimeSpan.FromMilliseconds(1))
				.Logger(_testLogger);
    }
}

