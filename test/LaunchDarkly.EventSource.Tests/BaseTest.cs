using System;
using System.Net.Http;
using LaunchDarkly.Logging;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.EventSource.Tests
{
    // [Collection] causes all the tests to be grouped together so they're not parallelized. Running
    // tests in parallel would make task scheduling very unpredictable, increasing the chances of
    // getting spurious timeouts.
    [Collection("all together")]
    public abstract class BaseTest
    {
        public static readonly Uri _uri = new Uri("http://test-uri");

        /// <summary>
        /// Tests can use this object wherever an <see cref="ILogAdapter"/> is needed, to
        /// direct log output to both 1. the Xunit test output buffer and 2. <see cref="_logCapture"/>.
        /// </summary>
        protected readonly ILogAdapter _testLogging;

        /// <summary>
        /// Tests can use this object whereever a <see cref="Logger"/> is needed, to
        /// direct log output to both 1. the Xunit test output buffer and 2. <see cref="_logCapture"/>.
        /// </summary>
        protected readonly Logger _testLogger;

        /// <summary>
        /// Captures all log output that is written to the test logger.
        /// </summary>
        protected readonly LogCapture _logCapture;

        /// <summary>
        /// Default constructor - does not use the Xunit test output buffer.
        /// </summary>
        public BaseTest()
        {
            _logCapture = Logs.Capture();
            _testLogging = _logCapture;
            _testLogger = _testLogging.Logger("");
        }

        /// <summary>
        /// Use this constructor if you want to be able to send logs to the Xunit test output buffer.
        /// Xunit will pass the <see cref="ITestOutputHelper"/> to the test subclass's constructor
        /// if you declare a parameter of that type.
        /// </summary>
        /// <param name="testOutput"></param>
        public BaseTest(ITestOutputHelper testOutput)
        {
            _logCapture = Logs.Capture();
            _testLogging = Logs.ToMultiple(
                Logs.ToMethod(testOutput.WriteLine),
                _logCapture
                );
            _testLogger = _testLogging.Logger("");
        }

        protected EventSource MakeEventSource(HttpMessageHandler httpHandler, Action<ConfigurationBuilder> modConfig = null)
        {
            var builder = Configuration.Builder(_uri)
                .HttpMessageHandler(httpHandler)
                .LogAdapter(_testLogging);
            AddBaseConfig(builder);
            modConfig?.Invoke(builder);
            return new EventSource(builder.Build());
        }

        /// <summary>
        /// Override this method to add configuration defaults to the behavior of
        /// <see cref="MakeEventSource(HttpMessageHandler, Action{ConfigurationBuilder})"/>.
        /// </summary>
        /// <param name="builder"></param>
        protected virtual void AddBaseConfig(ConfigurationBuilder builder) { }
    }
}
