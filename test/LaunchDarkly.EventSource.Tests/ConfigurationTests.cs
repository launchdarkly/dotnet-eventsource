using System;
using System.Net.Http;

using Xunit;


namespace LaunchDarkly.EventSource.Tests
{
    public class ConfigurationTests
    {
        private readonly Uri _uri = new Uri("http://test.com");

        [Fact]
        public void Configuration_constructor_throws_exception_when_uri_is_null()
        {
            var e = Record.Exception(() => new Configuration(null));

            Assert.NotNull(e);
            Assert.IsType<ArgumentNullException>(e);
        }

        [Fact]
        public void Configuration_constructor_throws_exception_when_http_client_and_messageHandler_is_provided()
        {
            var stubMessageHandler = new StubMessageHandler();
            var e = Record.Exception(() =>
                new Configuration(uri: _uri, 
                    httpClient: new HttpClient(stubMessageHandler), 
                    messageHandler: stubMessageHandler));

            Assert.IsType<ArgumentException>(e);
        }

        [Fact]
        public void Configuration_constructor_throws_exception_when_http_client_and_connectionTimeout_is_provided()
        {
            var stubMessageHandler = new StubMessageHandler();
            var e = Record.Exception(() =>
                new Configuration(uri: _uri, 
                    httpClient: new HttpClient(stubMessageHandler), 
                    connectionTimeout: TimeSpan.Zero));

            Assert.IsType<ArgumentException>(e);
        }

        [Fact]
        public void Configuration_constructor_throws_exception_when_connection_timeout_is_negative()
        {
            var e = Record.Exception(() => new Configuration(
                uri: _uri,
                connectionTimeout: new TimeSpan(-1)));

            Assert.NotNull(e);
            Assert.IsType<ArgumentOutOfRangeException>(e);
        }

        [Fact]
        public void Configuration_constructor_throws_exception_when_read_timeout_is_negative()
        {
            var e = Record.Exception(() => new Configuration(
                uri: _uri,
                readTimeout: new TimeSpan(-1)));

            Assert.NotNull(e);
            Assert.IsType<ArgumentOutOfRangeException>(e);
        }

        [Fact]
        public void Configuration_constructor_throws_exception_when_delay_retry_duration_exceeds_maximum_value()
        {
            var e = Record.Exception(() => new Configuration(
                uri: _uri,
                delayRetryDuration: TimeSpan.FromMilliseconds(30001)));

            Assert.NotNull(e);
            Assert.IsType<ArgumentOutOfRangeException>(e);
        }
    }
}
