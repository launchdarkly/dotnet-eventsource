using System;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// An abstraction of how <see cref="EventSource"/> should determine the delay
    /// between retry attempts when a stream fails.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default behavior, provided by {@link DefaultRetryDelayStrategy}, provides
    /// customizable exponential backoff and jitter. Applications may also create their own
    /// implementations of RetryDelayStrategy if they desire different behavior. It is
    /// generally a best practice to use backoff and jitter, to avoid a reconnect storm
    /// during a service interruption.
    /// </para>
    /// <para>
    /// Subclasses should be immutable. To implement strategies where the delay uses
    /// different parameters on each subsequent retry (such as exponential backoff),
    /// the strategy should return a new instance of its own class in
    /// <see cref="Result.Next"/>, rather than modifying the state of the existing
    /// instance. This makes it easy for EventSource to reset to the original delay
    /// state when appropriate by simply reusing the original instance.
    /// </para>
    /// </remarks>
    public abstract class RetryDelayStrategy
    {
        /// <summary>
        /// The return type of <see cref="RetryDelayStrategy.Apply(TimeSpan)"/>.
        /// </summary>
        public struct Result
        {
            /// <summary>
            /// The action that EventSource should take.
            /// </summary>
            public TimeSpan Delay { get; set; }

            /// <summary>
            /// The strategy instance to be used for the next retry, or null to use the
            /// same instance as last time.
            /// </summary>
            public RetryDelayStrategy Next { get; set; }
        }

        /// <summary>
        /// Applies the strategy to compute the appropriate retry delay.
        /// </summary>
        /// <param name="baseRetryDelay">the initial configured base delay</param>
        /// <returns>the result</returns>
        public abstract Result Apply(TimeSpan baseRetryDelay);

        /// <summary>
        /// Returns the default implementation, configured to use the default backoff
        /// and jitter.
        /// </summary>
        /// <remarks>
        /// You can call <see cref="DefaultRetryDelayStrategy"/> methods on this object
        /// to configure a strategy with different parameters.
        /// </remarks>
        public static DefaultRetryDelayStrategy Default =>
            DefaultRetryDelayStrategy.Instance;
    }
}
