using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using LaunchDarkly.TestHelpers.HttpTest;
using Xunit;
using Xunit.Abstractions;
using static System.Net.WebRequestMethods;

namespace LaunchDarkly.EventSource
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

            // The following line prevents intermittent test failures that can happen due to the low
            // default setting of ThreadPool.SetMinThreads causing new worker tasks to be severely
            // throttled: http://joeduffyblog.com/2006/07/08/clr-thread-pool-injection-stuttering-problems/
            // This makes it difficult to test things such as timeouts. We believe it not to be a real
            // issue in non-test scenarios, since the tests are starting and stopping an unusually large
            // number of async tasks in a way that regular use of EventSource would not do.
            ThreadPool.SetMinThreads(100, 100);
        }

        /// <summary>
        /// Use this constructor if you want to be able to send logs to the Xunit test output buffer.
        /// Xunit will pass the <see cref="ITestOutputHelper"/> to the test subclass's constructor
        /// if you declare a parameter of that type.
        /// </summary>
        /// <param name="testOutput"></param>
        public BaseTest(ITestOutputHelper testOutput) : this()
        {
            _testLogging = Logs.ToMultiple(
                Logs.ToMethod(testOutput.WriteLine),
                _logCapture
                );
            _testLogger = _testLogging.Logger("");
        }

        protected EventSource MakeEventSource(Uri uri, Action<ConfigurationBuilder> modConfig = null)
        {
            var builder = Configuration.Builder(uri)
                .LogAdapter(_testLogging);
            AddBaseConfig(builder);
            modConfig?.Invoke(builder);
            return new EventSource(builder.Build());
        }

        protected EventSource MakeEventSource(
            Uri uri,
            Func<HttpConnectStrategy, HttpConnectStrategy> modHttp = null,
            Action<ConfigurationBuilder> modConfig = null
            )
        {
            var http = ConnectStrategy.Http(uri);
            http = modHttp is null ? http : modHttp(http);
            var builder = Configuration.Builder(http)
                .LogAdapter(_testLogging);
            AddBaseConfig(builder);
            modConfig?.Invoke(builder);
            return new EventSource(builder.Build());
        }

        protected void WithServerAndEventSource(Handler handler, Action<HttpServer, EventSource> action) =>
            WithServerAndEventSource(handler, null, null, action);

        protected void WithServerAndEventSource(
            Handler handler,
            Func<HttpConnectStrategy, HttpConnectStrategy> modHttp,
            Action<ConfigurationBuilder> modConfig,
            Action<HttpServer, EventSource> action
            )
        {
            using (var server = HttpServer.Start(handler))
            {
                using (var es = MakeEventSource(server.Uri, modHttp, modConfig))
                {
                    action(server, es);
                }
            }
        }
        
        protected async Task WithServerAndEventSource(Handler handler, Func<HttpServer, EventSource, Task> action) =>
            await WithServerAndEventSource(handler, null, null, action);

        protected async Task WithServerAndEventSource(
            Handler handler,
            Func<HttpConnectStrategy, HttpConnectStrategy> modHttp,
            Action<ConfigurationBuilder> modConfig,
            Func<HttpServer, EventSource, Task> action
            )
        {
            using (var server = HttpServer.Start(handler))
            {
                using (var es = MakeEventSource(server.Uri, modHttp, modConfig))
                {
                    await action(server, es);
                }
            }
        }

        protected Task WithMockConnectEventSource(
            Action<MockConnectStrategy> configureMockConnect,
            Action<ConfigurationBuilder> configureEventSource,
            Func<MockConnectStrategy, EventSource, Task> action
            )
        {
            var mock = new MockConnectStrategy();
            configureMockConnect?.Invoke(mock);
            var builder = Configuration.Builder(mock)
                .LogAdapter(_testLogging);
            AddBaseConfig(builder);
            configureEventSource?.Invoke(builder);
            return action(mock, new EventSource(builder.Build()));
        }

        protected Task WithMockConnectEventSource(
            Action<MockConnectStrategy> configureMockConnect,
            Func<MockConnectStrategy, EventSource, Task> action
            ) => WithMockConnectEventSource(configureMockConnect, null, action);

        /// <summary>
        /// Override this method to add configuration defaults to the behavior of
        /// <see cref="MakeEventSource(HttpMessageHandler, Action{ConfigurationBuilder})"/>.
        /// </summary>
        /// <param name="builder"></param>
        protected virtual void AddBaseConfig(ConfigurationBuilder builder) { }

        protected static Handler StreamWithCommentThatStaysOpen =>
            Handlers.SSE.Start().Then(Handlers.SSE.Comment("")).Then(Handlers.SSE.LeaveOpen());

        protected static Handler EmptyStreamThatStaysOpen =>
            Handlers.SSE.Start().Then(Handlers.SSE.LeaveOpen());
    }
}
