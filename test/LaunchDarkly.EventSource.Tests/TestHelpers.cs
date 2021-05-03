using System;
using System.Collections.Generic;
using System.Text;
using LaunchDarkly.TestHelpers.HttpTest;
using Xunit;

namespace LaunchDarkly.EventSource.Tests
{
    public static class TestHelpers
    {
        public static Handler StartStream() => Handlers.SSE.Start();

        public static Handler LeaveStreamOpen() => Handlers.SSE.LeaveOpen();

        public static Handler WriteEvent(string s) => Handlers.WriteChunkString(s + "\n\n");

        public static Handler WriteEvent(MessageEvent e)
        {
            var s = new StringBuilder();
            if (e.Name != null)
            {
                s.Append("event:").Append(e.Name).Append("\n");
            }
            foreach (var line in e.Data.Split('\n'))
            {
                s.Append("data:").Append(line).Append("\n");
            }
            if (e.LastEventId != null)
            {
                s.Append("id:").Append(e.LastEventId).Append("\n");
            }
            return Handlers.WriteChunkString(s.ToString() + "\n");
        }

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
