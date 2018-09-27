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
        private int _reconnectAttempts;

        public ExponentialBackoffWithDecorrelation(TimeSpan minimumDelay, TimeSpan maximumDelay)
        {
            _minimumDelay = minimumDelay;
            _maximumDelay = maximumDelay;
        }

        /// <summary>
        /// Gets the next backoff duration and increments the reconnect attempt count
        /// </summary>
        public TimeSpan GetNextBackOff()
        {
            int nextDelay = Convert.ToInt32(Math.Min(_maximumDelay.TotalMilliseconds, _minimumDelay.TotalMilliseconds * Math.Pow(2, _reconnectAttempts)));
            nextDelay = nextDelay / 2 + _jitterer.Next(nextDelay) / 2;
            _reconnectAttempts++;
            return TimeSpan.FromMilliseconds(nextDelay);
        }

        public void ResetReconnectAttemptCount() {
            _reconnectAttempts = 0;
        }

        [Obsolete("IncrementReconnectAttemptCount is deprecated, use GetNextBackOff instead.")]
        public void IncrementReconnectAttemptCount() {
            _reconnectAttempts++;
        }
    }
}
