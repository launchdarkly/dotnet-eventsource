using System;
using System.Collections.Generic;
using System.Threading;
using LaunchDarkly.Logging;
using Xunit;

namespace LaunchDarkly.EventSource
{
    public class ConfigurationBuilderTests
    {
        private static readonly Uri uri = new Uri("http://test");

        [Fact]
        public void BuilderRejectsNullParameter()
        {
            Assert.ThrowsAny<ArgumentNullException>(() =>
                Configuration.Builder((Uri)null));
            Assert.ThrowsAny<ArgumentNullException>(() =>
                Configuration.Builder((ConnectStrategy)null));
        }

        [Fact]
        public void CanSetConnectStrategy()
        {
            var cs = ConnectStrategy.Http(uri);
            Assert.Same(cs, Configuration.Builder(cs).Build().ConnectStrategy);
        }

        [Fact]
        public void CanSetConnectStrategyWithUri()
        {
            var cs = Configuration.Builder(uri).Build();
            Assert.Equal(uri, Assert.IsType<HttpConnectStrategy>(cs.ConnectStrategy).Origin);
        }

        [Fact]
        public void CanSetErrorStrategy()
        {
            Assert.Same(ErrorStrategy.AlwaysThrow,
                Configuration.Builder(uri).Build().ErrorStrategy);

            var es = ErrorStrategy.AlwaysContinue;
            Assert.Same(es, Configuration.Builder(uri).ErrorStrategy(es).Build().ErrorStrategy);
        }

        [Fact]
        public void CanSetExpectFields()
        {
            Assert.Null(Configuration.Builder(uri).Build().ExpectFields);

            Assert.Equivalent(new HashSet<string> { "event", "id" },
                Configuration.Builder(uri).ExpectFields("event", "id").Build().ExpectFields);
        }

        [Fact]
        public void CanSetRetryDelayStrategy()
        {
            Assert.Same(RetryDelayStrategy.Default,
                Configuration.Builder(uri).Build().RetryDelayStrategy);

            var rs = RetryDelayStrategy.Default.BackoffMultiplier(4);
            Assert.Same(rs, Configuration.Builder(uri).RetryDelayStrategy(rs).Build().RetryDelayStrategy);
        }

        [Fact]
        public void InitialRetryDelayRetryHasDefault()
        {
            var b = Configuration.Builder(uri);
            Assert.Equal(Configuration.DefaultInitialRetryDelay, b.Build().InitialRetryDelay);
        }

        [Fact]
        public void BuilderSetsInitialRetryDelay()
        {
            var ts = TimeSpan.FromSeconds(9);
            var b = Configuration.Builder(uri).InitialRetryDelay(ts);
            Assert.Equal(ts, b.Build().InitialRetryDelay);
        }

        [Fact]
        public void NegativeInitialRetryDelayBecomesZero()
        {
            var ts = Timeout.InfiniteTimeSpan;
            var b = Configuration.Builder(uri).InitialRetryDelay(TimeSpan.FromSeconds(-9));
            Assert.Equal(TimeSpan.Zero, b.Build().InitialRetryDelay);
        }

        [Fact]
        public void LastEventIdDefaultsToNull()
        {
            var b = Configuration.Builder(uri);
            Assert.Null(b.Build().LastEventId);
        }

        [Fact]
        public void BuilderSetsLastEventId()
        {
            var b = Configuration.Builder(uri).LastEventId("abc");
            Assert.Equal("abc", b.Build().LastEventId);
        }

        [Fact]
        public void LoggerIsNeverNull()
        {
            var b = Configuration.Builder(uri);
            Assert.NotNull(b.Build().Logger);
        }

        [Fact]
        public void BuilderSetsLogAdapter()
        {
            var logMessages = Logs.Capture();
            var b = Configuration.Builder(uri).LogAdapter(logMessages);
            var logger = b.Build().Logger;
            logger.Info("hello");
            Assert.Equal(new List<LogCapture.Message>
            {
                new LogCapture.Message("EventSource", LogLevel.Info, "hello")
            }, logMessages.GetMessages());
        }

        [Fact]
        public void BuilderSetsLogger()
        {
            var logger = Logs.ToConsole.Logger("test");
            var b = Configuration.Builder(uri).Logger(logger);
            Assert.Same(logger, b.Build().Logger);
        }

        [Fact]
        public void CanSetStreamEventData()
        {
            Assert.False(Configuration.Builder(uri).Build().StreamEventData);

            Assert.True(Configuration.Builder(uri).StreamEventData(true).Build().StreamEventData);
        }
    }
}
