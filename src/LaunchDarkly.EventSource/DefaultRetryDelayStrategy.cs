using System;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// Default implementation of the retry delay strategy, providing exponential
    /// backoff and jitter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The algorithm is as follows:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// Start with the configured base delay as set by
    /// <see cref="ConfigurationBuilder.InitialRetryDelay(TimeSpan)"/>.
    /// </description></item>
    /// <item><description>
    /// On each subsequent attempt, multiply the base delay by the backoff multiplier,
    /// giving the current base delay.
    /// </description></item>
    /// <item><description>
    /// If there is a maximum delay, pin the current base delay to be no greater than
    /// the maximum.
    /// </description></item>
    /// <item><description>
    /// If the jitter multiplier is non-zero, the actual delay for each attempt is
    /// equal to the current base delay minus a pseudo-random number equal to that
    /// ratio times the current base delay. For instance, a jitter multiplier of
    /// 0.25 would mean that a base delay of 1000ms is changed to a value in the range
    /// [750ms, 1000ms].
    /// </description></item>
    /// </list>
    /// <para>
    /// This class is immutable. <see cref="RetryDelayStrategy.Default"/> returns the
    /// default instance. To change any parameters, call methods which return a modified
    /// instance:
    /// </para>
    /// <example><code>
    ///
    /// </code></example>
    /// </remarks>
    public class DefaultRetryDelayStrategy : RetryDelayStrategy
    {
        /// <summary>
        /// The default maximum delay: 30 seconds.
        /// </summary>
        public static readonly TimeSpan DefaultMaxDelay = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The default backoff multiplier: 2.
        /// </summary>
        public static readonly float DefaultBackoffMultiplier = 2;

        /// <summary>
        /// The default jitter multiplier: 0.5.
        /// </summary>
        public static readonly float DefaultJitterMultiplier = 0.5f;

        internal static readonly DefaultRetryDelayStrategy Instance =
            new DefaultRetryDelayStrategy(null, DefaultMaxDelay,
                DefaultBackoffMultiplier, DefaultJitterMultiplier);
        private static readonly Random _random = new Random();

        private readonly TimeSpan? _lastBaseDelay;
        private readonly TimeSpan? _maxDelay;
        private readonly float _backoffMultiplier;
        private readonly float _jitterMultiplier;

        private DefaultRetryDelayStrategy(
            TimeSpan? lastBaseDelay,
            TimeSpan? maxDelay,
            float backoffMultiplier,
            float jitterMultiplier
            )
        {
            _lastBaseDelay = lastBaseDelay;
            _maxDelay = maxDelay;
            _backoffMultiplier = backoffMultiplier == 0 ? 1 : backoffMultiplier;
            _jitterMultiplier = jitterMultiplier;
        }

        /// <summary>
        /// Returns a modified strategy with a specific maximum delay, or with no maximum.
        /// </summary>
        /// <param name="newMaxDelay">the new maximum delay, if any</param>
        /// <returns>a new instance with the specified maximum delay</returns>
        /// <seealso cref="DefaultMaxDelay"/>
        public DefaultRetryDelayStrategy MaxDelay(TimeSpan? newMaxDelay) =>
            new DefaultRetryDelayStrategy(
                _lastBaseDelay,
                newMaxDelay,
                _backoffMultiplier,
                _jitterMultiplier
                );

        /// <summary>
        /// Returns a modified strategy with a specific backoff multiplier. A multiplier of
        /// 1 means the base delay never changes, 2 means it doubles each time, etc. A
        /// value of zero is treated the same as 1.
        /// </summary>
        /// <param name="newBackoffMultiplier">the new backoff multiplier</param>
        /// <returns>a new instance with the specified backoff multiplier</returns>
        /// <seealso cref="DefaultBackoffMultiplier"/>
        public DefaultRetryDelayStrategy BackoffMultiplier(float newBackoffMultiplier) =>
            new DefaultRetryDelayStrategy(
                _lastBaseDelay,
                _maxDelay,
                newBackoffMultiplier,
                _jitterMultiplier
                );

        /// <summary>
        /// Returns a modified strategy with a specific jitter multiplier. A multiplier of
        /// 0.5 means each delay is reduced randomly by up to 50%, 0.25 means it is reduced
        /// randomly by up to 25%, etc. Zero means there is no jitter.
        /// </summary>
        /// <param name="newJitterMultiplier">the new jitter multiplier</param>
        /// <returns>a new instance with the specified jitter multiplier</returns>
        /// <seealso cref="DefaultJitterMultiplier"/>
        public DefaultRetryDelayStrategy JitterMultiplier(float newJitterMultiplier) =>
            new DefaultRetryDelayStrategy(
                _lastBaseDelay,
                _maxDelay,
                _backoffMultiplier,
                newJitterMultiplier
                );

        /// <summary>
        /// Called by EventSource to compute the next retry delay.
        /// </summary>
        /// <param name="baseRetryDelay">the current base delay</param>
        /// <returns>the result</returns>
        public override Result Apply(TimeSpan baseRetryDelay)
        {
            TimeSpan nextBaseDelay = _lastBaseDelay.HasValue ?
                TimeSpan.FromTicks((long)(_lastBaseDelay.Value.Ticks * _backoffMultiplier)) :
                baseRetryDelay;
            if (_maxDelay.HasValue && nextBaseDelay > _maxDelay.Value)
            {
                nextBaseDelay = _maxDelay.Value;
            }
            var adjustedDelay = nextBaseDelay;
            if (_jitterMultiplier > 0)
            {
                // 2^31 milliseconds is much longer than any reconnect time we would reasonably want to use, so we can pin this to int
                int maxTimeInt = nextBaseDelay.TotalMilliseconds > int.MaxValue ? int.MaxValue : (int)nextBaseDelay.TotalMilliseconds;
                int jitterRange = (int)(maxTimeInt * _jitterMultiplier);
                if (jitterRange != 0)
                {
                    lock (_random)
                    {
                        adjustedDelay -= TimeSpan.FromMilliseconds(_random.Next(jitterRange));
                    }
                }
            }
            RetryDelayStrategy updatedStrategy =
                new DefaultRetryDelayStrategy(nextBaseDelay, _maxDelay, _backoffMultiplier, _jitterMultiplier);
            return new Result { Delay = adjustedDelay, Next = updatedStrategy };
        }
    }
}

