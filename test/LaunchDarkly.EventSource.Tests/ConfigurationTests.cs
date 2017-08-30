using System;
using Xunit;


namespace LaunchDarkly.EventSource.Tests
{
    public class ConfigurationTests
    {
        private readonly Uri _uri = new Uri("http://test.com");

        [Fact]
        public void Configuration_uri_defaults_to_value_when_constructor_parameter_is_null()
        {
           var config = new Configuration(uri: null);

            Assert.NotNull(config.Uri);
        }

        [Fact]
        public void Configuration_constructor_throws_exception_when_connection_timeout_is_negative()
        {
            var e = Record.Exception(() => new Configuration(
                uri: _uri, 
                connectionTimeOut: new TimeSpan(-1)));
            
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
