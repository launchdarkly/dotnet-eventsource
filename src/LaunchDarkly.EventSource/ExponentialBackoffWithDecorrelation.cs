using System;
using System.Collections.Generic;
using System.Text;

namespace LaunchDarkly.EventSource
{
    public class ExponentialBackoffWithDecorrelation
    {
        private readonly TimeSpan _minimumDelay;
        private readonly TimeSpan _maximumDelay;
        private readonly Random _jitterer = new Random();
        private static int _reconnectAttempts;

        public ExponentialBackoffWithDecorrelation(TimeSpan minimumDelay, TimeSpan maximumDelay)
        {
            _minimumDelay = minimumDelay;
            _maximumDelay = maximumDelay;
        }

        public TimeSpan GetNextBackOff()
        {
            int nextDelay = Convert.ToInt32(Math.Min(_maximumDelay.TotalMilliseconds, _minimumDelay.TotalMilliseconds * Math.Pow(2, _reconnectAttempts++)));
            nextDelay = nextDelay / 2 + _jitterer.Next(nextDelay) / 2;
            return TimeSpan.FromMilliseconds(nextDelay);
        }

        public int GetReconnectAttemptCount() {
            return _reconnectAttempts;
        }

        public void IncrementReconnectAttemptCount() {
            _reconnectAttempts++;
        }
    }
}
