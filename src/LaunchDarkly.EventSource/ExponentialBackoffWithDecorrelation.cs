using System;

namespace LaunchDarkly.EventSource
{
    internal sealed class ExponentialBackoffWithDecorrelation
    {
        private readonly object _lock = new object();
        private readonly Random _randomSource;

        private TimeSpan _minimumDelay;
        private TimeSpan _maximumDelay;
        private int _backoffN;
        private TimeSpan? _serverDirectedMinDelay;

        public ExponentialBackoffWithDecorrelation(TimeSpan minimumDelay, TimeSpan maximumDelay)
            : this(minimumDelay, maximumDelay, new Random())
        {
        }

        /// <summary>
        /// Constructs an instance with a caller-supplied <see cref="Random"/>, so that tests can
        /// make jitter deterministic.
        /// </summary>
        internal ExponentialBackoffWithDecorrelation(TimeSpan minimumDelay, TimeSpan maximumDelay,
            Random randomSource)
        {
            _minimumDelay = NonNegative(minimumDelay);
            _maximumDelay = NonNegative(maximumDelay);
            _randomSource = randomSource ?? new Random();
        }

        /// <summary>
        /// Gets the next backoff duration, advancing n.
        /// </summary>
        public TimeSpan GetNextBackOff()
        {
            lock (_lock)
            {
                long unjittered = GetUnjitteredMillisecondsForN(_backoffN);

                // Random.Next takes an int bound. 2^31 milliseconds is far longer than any
                // reconnect delay we would use, so saturating here cannot affect a realistic
                // delay.
                int jitterBound = unjittered > int.MaxValue ? int.MaxValue : (int)unjittered;
                long delayMillis = unjittered / 2
                    + (jitterBound > 0 ? _randomSource.Next(jitterBound) / 2 : 0);

                _backoffN++;
                return TimeSpan.FromMilliseconds(delayMillis);
            }
        }

        /// <summary>
        /// Returns the delay in whole milliseconds for a given backoff <c>n</c>, before jitter is
        /// applied: <c>minimumDelay * 2^n</c>, limited to the maximum.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The caller must hold the lock. This reads the bounds without taking it because it is the
        /// inner core of <see cref="GetNextBackOff"/>, which already holds it; taking it here would
        /// only be a re-entrant acquisition on the hot path. Anything needing a synchronized read of
        /// the bounds on its own should use the accessors below, which take the lock themselves.
        /// </para>
        /// <para>
        /// <c>n</c> is compared against the ceiling rather than used to compute a value that is
        /// then clamped, so no intermediate ever exceeds the maximum. That matters because
        /// <c>n</c> is unbounded: a long outage keeps incrementing it, and computing
        /// <c>minimumDelay * 2^n</c> directly would overflow.
        /// </para>
        /// </remarks>
        internal long GetUnjitteredMillisecondsForN(int n)
        {
            long min = (_serverDirectedMinDelay ?? _minimumDelay).Ticks
                / TimeSpan.TicksPerMillisecond;
            long max = _maximumDelay.Ticks / TimeSpan.TicksPerMillisecond;

            if (min <= 0 || max <= 0)
            {
                return 0;
            }
            if (n <= 0)
            {
                return min > max ? max : min;
            }

            // Shifting the ceiling down cannot overflow, whereas shifting the minimum up can.
            // The n >= 63 test is required rather than defensive: C# masks a 64-bit shift count
            // to its low 6 bits, so `max >> 64` would silently mean `max >> 0`.
            if (n >= 63 || min > (max >> n))
            {
                return max;
            }
            return min << n;
        }

        /// <summary>
        /// Applies a server-directed reconnection time received via the SSE <c>retry:</c> field,
        /// replacing the minimum delay used to compute subsequent backoffs.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The override takes precedence over both the configured minimum and any minimum
        /// installed by <see cref="SetBounds"/>. It is sticky: it survives bounds changes and
        /// <see cref="ResetBackoffN"/>, and is replaced only by a later server-directed value.
        /// </para>
        /// <para>
        /// The maximum is never affected, so the ceiling that bounds reconnection keeps applying.
        /// </para>
        /// <para>
        /// n resets, so the next attempt uses the directed value itself rather than continuing to
        /// double from wherever the progression had reached.
        /// </para>
        /// <para>
        /// Negative values become zero. No upper bound is applied here; a caller taking a value
        /// from an untrusted source is responsible for capping it first.
        /// </para>
        /// </remarks>
        /// <param name="minDelay">the server-directed reconnection time</param>
        public void SetServerDirectedMinDelay(TimeSpan minDelay)
        {
            var value = NonNegative(minDelay);
            lock (_lock)
            {
                _serverDirectedMinDelay = value;
                _backoffN = 0;
            }
        }

        /// <summary>
        /// Replaces the delay bounds.
        /// </summary>
        /// <remarks>
        /// If the new bounds differ from the current ones, n resets so that the
        /// next delay starts from the new minimum. If they are the same, this does nothing, which
        /// makes repeated calls with identical values idempotent -- a caller that reapplies the
        /// same bounds on every failure will not pin the delay at the minimum.
        /// </remarks>
        /// <param name="minimumDelay">the new minimum delay; negative values become zero</param>
        /// <param name="maximumDelay">the new maximum delay; negative values become zero</param>
        public void SetBounds(TimeSpan minimumDelay, TimeSpan maximumDelay)
        {
            var min = NonNegative(minimumDelay);
            var max = NonNegative(maximumDelay);
            lock (_lock)
            {
                if (min == _minimumDelay && max == _maximumDelay)
                {
                    return;
                }
                _minimumDelay = min;
                _maximumDelay = max;
                _backoffN = 0;
            }
        }

        /// <summary>
        /// Resets n so the next delay starts from the current minimum, without
        /// changing the bounds.
        /// </summary>
        public void ResetBackoffN()
        {
            lock (_lock)
            {
                _backoffN = 0;
            }
        }

        // Accessors for tests.

        internal int GetBackoffN()
        {
            lock (_lock)
            {
                return _backoffN;
            }
        }

        internal TimeSpan? GetServerDirectedMinDelay()
        {
            lock (_lock)
            {
                return _serverDirectedMinDelay;
            }
        }

        internal TimeSpan GetMinimumDelay()
        {
            lock (_lock)
            {
                return _minimumDelay;
            }
        }

        internal TimeSpan GetMaximumDelay()
        {
            lock (_lock)
            {
                return _maximumDelay;
            }
        }

        // Matches the clamping behavior of ConfigurationBuilder's retry-delay setters: a negative
        // duration becomes zero rather than throwing.
        private static TimeSpan NonNegative(TimeSpan t) => t < TimeSpan.Zero ? TimeSpan.Zero : t;
    }
}
