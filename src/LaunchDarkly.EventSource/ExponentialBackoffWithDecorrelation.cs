using System;
using System.Collections.Generic;
using System.Text;

namespace LaunchDarkly.EventSource
{
    internal class ExponentialBackoffWithDecorrelation
    {
        private readonly double _minimumDelay;
        private readonly double _maximumDelay;
        private double _currentDelay;
        private readonly Random _jitterer = new Random();

        public ExponentialBackoffWithDecorrelation(double minimumDelay, double maximumDelay)
        {
            _minimumDelay = minimumDelay;
            _maximumDelay = maximumDelay;
            _currentDelay = _minimumDelay;
        }

        public TimeSpan GetBackOff()
        {
            var nextJitter = _jitterer.NextDouble();
            var nextDelay = _currentDelay;

            nextDelay = Math.Min(_maximumDelay, Math.Max(_minimumDelay, _currentDelay * 3 * nextJitter));

            if (nextDelay == _currentDelay)
                nextDelay = GetBackOff().TotalMilliseconds;

            _currentDelay = nextDelay;

            return TimeSpan.FromMilliseconds(_currentDelay);
        }


    }
}
