using System;
using System.Collections.Generic;
using System.Text;

namespace LaunchDarkly.EventSource
{
    internal class ExponentialBackoffWithDecorrelation
    {
        private readonly double _minimumDelay;
    private readonly double _maximumDelay;
    private readonly Random _jitterer = new Random();

        public ExponentialBackoffWithDecorrelation(double minimumDelay, double maximumDelay)
        {
            _minimumDelay = minimumDelay;
            _maximumDelay = maximumDelay;
        }

        public TimeSpan GetBackOff(int reconnectAttempts)
        {
            double nextDelay = Math.Min(_maximumDelay, _minimumDelay * Math.Pow(2, reconnectAttempts));
            nextDelay = nextDelay / 2 + _jitterer.Next((int)nextDelay) / 2;
            return TimeSpan.FromMilliseconds(nextDelay);
        }


    }
}
