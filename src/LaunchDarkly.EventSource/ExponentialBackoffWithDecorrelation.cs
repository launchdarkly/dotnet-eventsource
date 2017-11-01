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

        public ExponentialBackoffWithDecorrelation(TimeSpan minimumDelay, TimeSpan maximumDelay)
        {
            _minimumDelay = minimumDelay;
            _maximumDelay = maximumDelay;
        }

        public TimeSpan GetBackOff(int reconnectAttempts)
        {
            int nextDelay = Convert.ToInt32(Math.Min(_maximumDelay.TotalMilliseconds, _minimumDelay.TotalMilliseconds * Math.Pow(2, reconnectAttempts)));
            nextDelay = nextDelay / 2 + _jitterer.Next(nextDelay) / 2;
            return TimeSpan.FromMilliseconds(nextDelay);
        }


    }
}
