using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.EventSource.Events;
using LaunchDarkly.EventSource.Internal;
using LaunchDarkly.TestHelpers.HttpTest;
using Xunit;

namespace LaunchDarkly.EventSource
{
    public static class TestHelpers
    {
        public static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

        public static Handler StartStream() => Handlers.SSE.Start();

        public static Handler LeaveStreamOpen() => Handlers.SSE.LeaveOpen();

        public static Handler WriteComment(string s) => Handlers.SSE.Comment(s);

        public static Handler WriteEvent(string s) => Handlers.SSE.Event(s);

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

        public static T RequireValue<T>(BlockingCollection<T> queue, TimeSpan timeout)
        {
            Assert.True(queue.TryTake(out var value, timeout), "timed out waiting for value");
            return value;
        }

        public static T RequireValue<T>(BlockingCollection<T> queue) =>
            RequireValue(queue, TimeSpan.FromSeconds(1));

        public static string AsSSEData(this MessageEvent e)
        {
            var sb = new StringBuilder();
            sb.Append("event:").Append(e.Name).Append("\n");
            foreach (var s in e.Data.Split('\n'))
            {
                sb.Append("data:").Append(s).Append("\n");
            }
            if (e.LastEventId != null)
            {
                sb.Append("id:").Append(e.LastEventId).Append("\n");
            }
            sb.Append("\n");
            return sb.ToString();
        }

        // The WithTimeout methods are used in tests only, because the timeout behavior is
        // is not what we would want in a real application: if it times out, the underlying
        // task is still trying to parse an event so the EventSource is no longer in a valid
        // state. A real timeout method would require different logic in EventParser, because
        // currently EventParser is not able to resume reading a partially-read event.

        public static async Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout)
        {
            try
            {
                return await AsyncHelpers.DoWithTimeout(timeout, new CancellationToken(), _ => task);
            }
            catch (ReadTimeoutException)
            {
                throw new Exception("timed out");
            }
        }

        public static async Task WithTimeout(this Task task, TimeSpan timeout)
        {
            try
            {
                await AsyncHelpers.DoWithTimeout(timeout, new CancellationToken(), async _ =>
                {
                    await task;
                    return true;
                });
            }
            catch (ReadTimeoutException)
            {
                throw new Exception("timed out");
            }
        }

        public static Task<T> WithTimeout<T>(this Task<T> task) => task.WithTimeout(OneSecond);

        public static Task WithTimeout(this Task task) => task.WithTimeout(OneSecond);
    }
}
