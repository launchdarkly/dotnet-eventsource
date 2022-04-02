using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using Xunit;
using LaunchDarkly.Logging;

namespace LaunchDarkly.EventSource.Tests
{
    public class ConfigurationBuilderTests
    {
        private static readonly Uri uri = new Uri("http://test");

        [Fact]
        public void BuilderSetsUri()
        {
            var b = Configuration.Builder(uri);
            Assert.Equal(uri, b.Build().Uri);
        }

        [Fact]
        public void BuilderRejectsNullUri()
        {
            var e = Record.Exception(() => Configuration.Builder(null));
            Assert.IsType<ArgumentNullException>(e);
        }

#pragma warning disable 0618
        [Fact]
        public void DeprecatedConnectionTimeoutHasDefault()
        {
            var b = Configuration.Builder(uri);
            Assert.Equal(Configuration.DefaultConnectionTimeout, b.Build().ConnectionTimeout);
            Assert.Equal(Configuration.DefaultConnectionTimeout, b.Build().ResponseStartTimeout);
        }

        [Fact]
        public void BuilderSetsDeprecatedConnectionTimeout()
        {
            var ts = TimeSpan.FromSeconds(9);
            var b = Configuration.Builder(uri).ConnectionTimeout(ts);
            Assert.Equal(ts, b.Build().ConnectionTimeout);
            Assert.Equal(ts, b.Build().ResponseStartTimeout);
        }

        [Fact]
        public void DeprecatedConnectionTimeoutCanBeInfinite()
        {
            var ts = Timeout.InfiniteTimeSpan;
            var b = Configuration.Builder(uri).ConnectionTimeout(ts);
            Assert.Equal(ts, b.Build().ConnectionTimeout);
            Assert.Equal(ts, b.Build().ResponseStartTimeout);
        }

        [Fact]
        public void AnyNegativeDeprecatedConnectionTimeoutIsInfinite()
        {
            var ts = TimeSpan.FromSeconds(-9);
            var b = Configuration.Builder(uri).ConnectionTimeout(ts);
            Assert.Equal(Timeout.InfiniteTimeSpan, b.Build().ConnectionTimeout);
            Assert.Equal(Timeout.InfiniteTimeSpan, b.Build().ResponseStartTimeout);
        }
#pragma warning restore 0618

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
        public void MaxRetryDelayHasDefault()
        {
            var b = Configuration.Builder(uri);
            Assert.Equal(Configuration.DefaultMaxRetryDelay, b.Build().MaxRetryDelay);
        }

        [Fact]
        public void BuilderSetsMaxRetryDelay()
        {
            var ts = TimeSpan.FromSeconds(9);
            var b = Configuration.Builder(uri).MaxRetryDelay(ts);
            Assert.Equal(ts, b.Build().MaxRetryDelay);
        }

        [Fact]
        public void NegativeMaxRetryDelayBecomesZero()
        {
            var ts = Timeout.InfiniteTimeSpan;
            var b = Configuration.Builder(uri).MaxRetryDelay(TimeSpan.FromSeconds(-9));
            Assert.Equal(TimeSpan.Zero, b.Build().MaxRetryDelay);
        }

        [Fact]
        public void ReadTimeoutHasDefault()
        {
            var b = Configuration.Builder(uri);
            Assert.Equal(Configuration.DefaultReadTimeout, b.Build().ReadTimeout);
        }

        [Fact]
        public void BuilderSetsReadTimeout()
        {
            var ts = TimeSpan.FromSeconds(9);
            var b = Configuration.Builder(uri).ReadTimeout(ts);
            Assert.Equal(ts, b.Build().ReadTimeout);
        }

        [Fact]
        public void ReadTimeoutCanBeInfinite()
        {
            var ts = Timeout.InfiniteTimeSpan;
            var b = Configuration.Builder(uri).ReadTimeout(ts);
            Assert.Equal(ts, b.Build().ReadTimeout);
        }

        [Fact]
        public void AnyNegativeReadTimeoutBecomesInfinite()
        {
            var ts = TimeSpan.FromSeconds(-9);
            var b = Configuration.Builder(uri).ReadTimeout(ts);
            Assert.Equal(Timeout.InfiniteTimeSpan, b.Build().ReadTimeout);
        }
        [Fact]
        public void ResponseStartTimeoutHasDefault()
        {
            var b = Configuration.Builder(uri);
            Assert.Equal(Configuration.DefaultResponseStartTimeout, b.Build().ResponseStartTimeout);
        }

        [Fact]
        public void BuilderSetsResponseStartTimeout()
        {
            var ts = TimeSpan.FromSeconds(9);
            var b = Configuration.Builder(uri).ResponseStartTimeout(ts);
            Assert.Equal(ts, b.Build().ResponseStartTimeout);
        }

        [Fact]
        public void ResponseStartTimeoutCanBeInfinite()
        {
            var ts = Timeout.InfiniteTimeSpan;
            var b = Configuration.Builder(uri).ResponseStartTimeout(ts);
            Assert.Equal(ts, b.Build().ResponseStartTimeout);
        }

        [Fact]
        public void AnyNegativeResponseStartTimeoutIsInfinite()
        {
            var ts = TimeSpan.FromSeconds(-9);
            var b = Configuration.Builder(uri).ResponseStartTimeout(ts);
            Assert.Equal(Timeout.InfiniteTimeSpan, b.Build().ResponseStartTimeout);
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
        public void RequestHeadersDefaultToEmptyDictionary()
        {
            var b = Configuration.Builder(uri);
            Assert.Equal(new Dictionary<string, string>(), b.Build().RequestHeaders);
        }

        [Fact]
        public void BuilderSetsRequestHeaders()
        {
            var h = new Dictionary<string, string>();
            h["a"] = "1";
            var b = Configuration.Builder(uri).RequestHeaders(h);
            Assert.Equal(h, b.Build().RequestHeaders);
        }

        [Fact]
        public void BuilderSetsIndividualRequestHeaders()
        {
            var h = new Dictionary<string, string>();
            h["a"] = "1";
            h["b"] = "2";
            var b = Configuration.Builder(uri).RequestHeader("a", "1").RequestHeader("b", "2");
            Assert.Equal(h, b.Build().RequestHeaders);
        }

        [Fact]
        public void MessageHandlerDefaultsToNull()
        {
            Assert.Null(Configuration.Builder(uri).Build().HttpMessageHandler);
        }

        [Fact]
        public void BuilderSetsMessageHandler()
        {
            var h = new HttpClientHandler();
            var b = Configuration.Builder(uri).HttpMessageHandler(h);
            Assert.Same(h, b.Build().HttpMessageHandler);
        }

        [Fact]
        public void HttpClientDefaultsToNull()
        {
            Assert.Null(Configuration.Builder(uri).Build().HttpClient);
        }

        [Fact]
        public void BuilderSetsHttpClient()
        {
            var h = new HttpClient();
            var b = Configuration.Builder(uri).HttpClient(h);
            Assert.Same(h, b.Build().HttpClient);
        }
        
        [Fact]
        public void MethodDefaultsToGet()
        {
            var b = Configuration.Builder(uri);
            Assert.Equal(HttpMethod.Get, b.Build().Method);
        }

        [Fact]
        public void BuilderSetsMethod()
        {
            var b = Configuration.Builder(uri).Method(HttpMethod.Post);
            Assert.Equal(HttpMethod.Post, b.Build().Method);
        }

        [Fact]
        public void RequestBodyFactoryDefaultsToNull()
        {
            var b = Configuration.Builder(uri);
            Assert.Null(b.Build().RequestBodyFactory);
        }

        [Fact]
        public void BuilderSetsRequestBodyFactory()
        {
            Func<HttpContent> f = () => new StringContent("x");
            var b = Configuration.Builder(uri).RequestBodyFactory(f);
            Assert.Same(f, b.Build().RequestBodyFactory);
        }

        [Fact]
        public void BuilderSetsRequestBodyString()
        {
            var b = Configuration.Builder(uri).RequestBody("x", "text/plain");
            var c = b.Build().RequestBodyFactory();
            Assert.IsType<StringContent>(c);
        }

        [Fact]
        public void BuilderSetsHttpRequestModifier()
        {
            Action<HttpRequestMessage> action = request => { };
            var b = Configuration.Builder(uri).HttpRequestModifier(action);
            Assert.Equal(action, b.Build().HttpRequestModifier);
        }
    }
}
