using Common.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using Xunit;

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

        [Fact]
        public void ConnectionTimeoutHasDefault()
        {
            var b = Configuration.Builder(uri);
            Assert.Equal(Configuration.DefaultConnectionTimeout, b.Build().ConnectionTimeout);
        }

        [Fact]
        public void BuilderSetsConnectionTimeout()
        {
            var ts = TimeSpan.FromSeconds(9);
            var b = Configuration.Builder(uri).ConnectionTimeout(ts);
            Assert.Equal(ts, b.Build().ConnectionTimeout);
        }

        [Fact]
        public void ConnectionTimeoutCanBeInfinite()
        {
            var ts = Timeout.InfiniteTimeSpan;
            var b = Configuration.Builder(uri).ConnectionTimeout(ts);
            Assert.Equal(ts, b.Build().ConnectionTimeout);
        }

        [Fact]
        public void ConnectionTimeoutCannotBeNegative()
        {
            var ts = TimeSpan.FromSeconds(-9);
            var b = Configuration.Builder(uri);
            var e = Record.Exception(() => b.ConnectionTimeout(ts));
            Assert.IsType<ArgumentOutOfRangeException>(e);
        }

        [Fact]
        public void DelayRetryDurationHasDefault()
        {
            var b = Configuration.Builder(uri);
            Assert.Equal(Configuration.DefaultDelayRetryDuration, b.Build().DelayRetryDuration);
        }

        [Fact]
        public void BuilderSetsDelayRetryDuration()
        {
            var ts = TimeSpan.FromSeconds(9);
            var b = Configuration.Builder(uri).DelayRetryDuration(ts);
            Assert.Equal(ts, b.Build().DelayRetryDuration);
        }

        [Fact]
        public void DelayRetryDurationCannotBeInfinite()
        {
            var ts = Timeout.InfiniteTimeSpan;
            var b = Configuration.Builder(uri);
            var e = Record.Exception(() => b.DelayRetryDuration(ts));
            Assert.IsType<ArgumentOutOfRangeException>(e);
        }

        [Fact]
        public void DelayRetryDurationCannotBeNegative()
        {
            var ts = TimeSpan.FromSeconds(-9);
            var b = Configuration.Builder(uri);
            var e = Record.Exception(() => b.DelayRetryDuration(ts));
            Assert.IsType<ArgumentOutOfRangeException>(e);
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
        public void ReadTimeoutCannotBeInfinite()
        {
            var ts = Timeout.InfiniteTimeSpan;
            var b = Configuration.Builder(uri);
            var e = Record.Exception(() => b.ReadTimeout(ts));
            Assert.IsType<ArgumentOutOfRangeException>(e);
        }

        [Fact]
        public void ReadTimeoutCannotBeNegative()
        {
            var ts = TimeSpan.FromSeconds(-9);
            var b = Configuration.Builder(uri);
            var e = Record.Exception(() => b.ReadTimeout(ts));
            Assert.IsType<ArgumentOutOfRangeException>(e);
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
        public void LoggerDefaultsToNull()
        {
            var b = Configuration.Builder(uri);
            Assert.Null(b.Build().Logger);
        }

        [Fact]
        public void BuilderSetsLog()
        {
            ILog log = LogManager.GetLogger("test");
            var b = Configuration.Builder(uri).Logger(log);
            Assert.Same(log, b.Build().Logger);
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
            Assert.Null(Configuration.Builder(uri).Build().MessageHandler);
        }

        [Fact]
        public void BuilderSetsMessageHandler()
        {
            var h = new HttpClientHandler();
            var b = Configuration.Builder(uri).MessageHandler(h);
            Assert.Same(h, b.Build().MessageHandler);
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
            Configuration.HttpContentFactory f = () => new StringContent("x");
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
    }
}
