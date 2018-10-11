using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace LaunchDarkly.EventSource.Tests
{
    public class ExponentialBackoffWithDecorrelationTests
    {
        [Fact]
        public void Exponential_backoff_should_not_exceed_maximum()
        {
            TimeSpan max = TimeSpan.FromMilliseconds(30000);
            ExponentialBackoffWithDecorrelation expo =
                new ExponentialBackoffWithDecorrelation(TimeSpan.FromMilliseconds(1000), max);

            var backoff = expo.GetNextBackOff();

            Assert.True(backoff <= max);
        }

        [Fact]
        public void Exponential_backoff_should_not_exceed_maximum_in_test_loop()
        {
            TimeSpan max = TimeSpan.FromMilliseconds(30000);

            ExponentialBackoffWithDecorrelation expo =
                new ExponentialBackoffWithDecorrelation(TimeSpan.FromMilliseconds(1000), max);

            for (int i = 0; i < 100; i++)
            {

                var backoff = expo.GetNextBackOff();

                Assert.True(backoff <= max);
            }

        }

        [Fact]
        public void Exponential_backoff_should_reset_when_reconnect_count_resets()
        {
            TimeSpan max = TimeSpan.FromMilliseconds(30000);

            ExponentialBackoffWithDecorrelation expo =
                new ExponentialBackoffWithDecorrelation(TimeSpan.FromMilliseconds(1000), max);

            for (int i = 0; i < 100; i++)
            {
                var backoff = expo.GetNextBackOff();
            }
            expo.ResetReconnectAttemptCount();
            // Backoffs use jitter, so assert that the reset backoff time isn't more than double the minimum
            Assert.True(expo.GetNextBackOff() <= TimeSpan.FromMilliseconds(2000));
        }
    }
}
