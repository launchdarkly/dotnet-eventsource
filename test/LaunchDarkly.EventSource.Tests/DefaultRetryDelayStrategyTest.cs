using System;
using Xunit;

namespace LaunchDarkly.EventSource
{
    public class DefaultRetryDelayStrategyTest
    {
        [Fact]
        public void BackoffWithNoJitterAndNoMax()
        {
            var baseDelay = TimeSpan.FromMilliseconds(4);

            var s = RetryDelayStrategy.Default.
                BackoffMultiplier(2).JitterMultiplier(0).
                MaxDelay(null);

            var r1 = s.Apply(baseDelay);
            Assert.Equal(baseDelay, r1.Delay);

            var last = baseDelay;
            var nextStrategy = r1.Next;
            for (int i = 0; i < 4; i++)
            {
                var r = nextStrategy.Apply(baseDelay);
                Assert.Equal(last.Multiply(2), r.Delay);
                last = r.Delay;
                nextStrategy = r.Next;
            }
        }

        [Fact]
        public void BackoffWithNoJitterAndMax()
        {
            var baseDelay = TimeSpan.FromMilliseconds(4);
            var max = baseDelay.Multiply(4) + TimeSpan.FromMilliseconds(3);

            var s = RetryDelayStrategy.Default.
                BackoffMultiplier(2).JitterMultiplier(0).
                MaxDelay(max);

            var r1 = s.Apply(baseDelay);
            Assert.Equal(baseDelay, r1.Delay);

            var r2 = r1.Next.Apply(baseDelay);
            Assert.Equal(baseDelay.Multiply(2), r2.Delay);

            var r3 = r2.Next.Apply(baseDelay);
            Assert.Equal(baseDelay.Multiply(4), r3.Delay);

            var r4 = r3.Next.Apply(baseDelay);
            Assert.Equal(max, r4.Delay);
        }

        [Fact]
        public void NoBackoffAndNoJitter()
        {
            var baseDelay = TimeSpan.FromMilliseconds(4);

            RetryDelayStrategy s = RetryDelayStrategy.Default.
                BackoffMultiplier(1).JitterMultiplier(0);

            var r1 = s.Apply(baseDelay);
            Assert.Equal(baseDelay, r1.Delay);

            var r2 = r1.Next.Apply(baseDelay);
            Assert.Equal(baseDelay, r2.Delay);

            var r3 = r2.Next.Apply(baseDelay);
            Assert.Equal(baseDelay, r3.Delay);
        }

        [Fact]
        public void BackoffWithJitter()
        {
            var baseDelay = TimeSpan.FromMilliseconds(4);
            var specifiedBackoff = 2;
            TimeSpan max = baseDelay.Multiply(specifiedBackoff).Multiply(specifiedBackoff)
                + TimeSpan.FromMilliseconds(3);
            float specifiedJitter = 0.25f;

            var s = RetryDelayStrategy.Default
                .BackoffMultiplier(specifiedBackoff)
                .JitterMultiplier(specifiedJitter)
                .MaxDelay(max);

            var r1 = VerifyJitter(s, baseDelay, baseDelay, specifiedJitter);
            var r2 = VerifyJitter(r1.Next, baseDelay, baseDelay.Multiply(2), specifiedJitter);
            var r3 = VerifyJitter(r2.Next, baseDelay, baseDelay.Multiply(4), specifiedJitter);
            VerifyJitter(r3.Next, baseDelay, max, specifiedJitter);
        }

        [Fact]
        public void DefaultBackoff()
        {
            var baseDelay = TimeSpan.FromMilliseconds(4);

            var s = RetryDelayStrategy.Default
                .JitterMultiplier(0);

            var r1 = s.Apply(baseDelay);
            Assert.Equal(baseDelay, r1.Delay);

            var r2 = r1.Next.Apply(baseDelay);
            Assert.Equal(baseDelay.Multiply(DefaultRetryDelayStrategy.DefaultBackoffMultiplier),
                r2.Delay);
        }

        [Fact]
        public void DefaultJitter()
        {
            var baseDelay = TimeSpan.FromMilliseconds(4);

            var s = RetryDelayStrategy.Default;

            VerifyJitter(s, baseDelay, baseDelay, DefaultRetryDelayStrategy.DefaultJitterMultiplier);
        }

        private RetryDelayStrategy.Result VerifyJitter(
            RetryDelayStrategy strategy,
            TimeSpan baseDelay,
            TimeSpan baseWithBackoff,
            float specifiedJitter
            )
        {
            // We can't 100% prove that it's using the expected jitter ratio, since the result
            // is pseudo-random, but we can at least prove that repeated computations don't
            // fall outside the expected range and aren't all equal.
            RetryDelayStrategy.Result? lastResult = null;
            var atLeastOneWasDifferent = false;
            for (int i = 0; i < 100; i++)
            {
                var result = strategy.Apply(baseDelay);
                Assert.InRange(
                        result.Delay,
                        baseWithBackoff - (baseWithBackoff.Multiply(specifiedJitter)),
                        baseWithBackoff
                        );
                if (lastResult.HasValue && !atLeastOneWasDifferent)
                {
                    atLeastOneWasDifferent = result.Delay != lastResult.Value.Delay;
                }
                lastResult = result;
            }
            return lastResult.Value;
        }

    }

    static class TimeSpanExtensions
    {
        // The built-in TimeSpan.Multiply() is not available in .NET Framework
        public static TimeSpan Multiply(this TimeSpan t, double d) =>
            TimeSpan.FromTicks((long)(t.Ticks * d));
    }
}

