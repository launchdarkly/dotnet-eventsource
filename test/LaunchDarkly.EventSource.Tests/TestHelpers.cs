using System;
using System.Collections.Generic;
using Xunit;

namespace LaunchDarkly.EventSource.Tests
{
    public static class TestHelpers
    {
        public static void AssertBackoffsAlwaysIncrease(List<TimeSpan> backoffs, int desiredCount)
        {
            Assert.InRange(backoffs.Count, desiredCount, 100);
            for (var i = 0; i < desiredCount - 1; i++)
            {
                Assert.NotEqual(backoffs[i], backoffs[i + 1]);
                Assert.True(backoffs[i + 1] > backoffs[i]);
            }
        }
    }
}
