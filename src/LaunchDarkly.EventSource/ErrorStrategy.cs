using System;
using System.Collections.Generic;
using LaunchDarkly.EventSource.Events;
using LaunchDarkly.EventSource.Exceptions;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// An abstraction of how to determine whether a stream failure should be thrown to the
    /// caller as an exception, or treated as an event.
    /// </summary>
    /// <para>
    /// Instances of this class should be immutable.
    /// </para>
    /// <seealso cref="ConfigurationBuilder.ErrorStrategy(ErrorStrategy)"/>
    public abstract class ErrorStrategy
    {
        /// <summary>
        /// Specifies that EventSource should always throw an exception if there is an error.
        /// This is the default behavior if you do not configure another.
        /// </summary>
        public static ErrorStrategy AlwaysThrow { get; } = new Invariant(Action.Throw);

        /// <summary>
        /// Specifies that EventSource should never throw an exception, but should return all
        /// errors as <see cref="FaultEvent"/>s. Be aware that using this mode could cause
        /// <see cref="EventSource.StartAsync"/> to block indefinitely if connections never succeed.
        /// </summary>
        public static ErrorStrategy AlwaysContinue { get; } = new Invariant(Action.Continue);

        /// <summary>
        /// Describes the possible actions EventSource could take after an error.
        /// </summary>
        public enum Action
        {
            /// <summary>
            /// Indicates that EventSource should throw an exception from whatever reading
            /// method was called (<see cref="EventSource.StartAsync"/>,
            /// <see cref="EventSource.ReadMessageAsync"/>, etc.).
            /// </summary>
            Throw,

            /// <summary>
            /// Indicates that EventSource should not throw an exception, but instead return a
            /// <see cref="FaultEvent"/> to the caller. If the caller continues to read from the
            /// failed stream after that point, EventSource will try to reconnect to the stream.
            /// </summary>
            Continue
        }

        /// <summary>
        /// The return type of <see cref="ErrorStrategy.Apply(Exception)"/>.
        /// </summary>
        public struct Result
        {
            /// <summary>
            /// The action that EventSource should take.
            /// </summary>
            public Action Action { get; set; }

            /// <summary>
            /// The strategy instance to be used for the next retry, or null to use the
            /// same instance as last time.
            /// </summary>
            public ErrorStrategy Next { get; set; }
        }

        /// <summary>
        /// Applies the strategy to determine whether to retry after a failure.
        /// </summary>
        /// <param name="exception">describes the failure</param>
        /// <returns>the result</returns>
        public abstract Result Apply(Exception exception);

        /// <summary>
        /// Specifies that EventSource should automatically retry after a failure for up to this
        /// number of consecutive attempts, but should throw an exception after that point.
        /// </summary>
        /// <param name="maxAttempts">the maximum number of consecutive retries</param>
        /// <returns>a strategy to be passed to <see cref="ConfigurationBuilder.ErrorStrategy(ErrorStrategy)"/>
        /// </returns>
        public static ErrorStrategy ContinueWithMaxAttempts(int maxAttempts) =>
            new MaxAttemptsImpl(maxAttempts, 0);

        /// <summary>
        /// Specifies that EventSource should automatically retry after a failure and can retry
        /// repeatedly until this amount of time has elapsed, but should throw an exception after
        /// that point.
        /// </summary>
        /// <param name="maxTime">the time limit</param>
        /// <returns>a strategy to be passed to <see cref="ConfigurationBuilder.ErrorStrategy(ErrorStrategy)"/>
        /// </returns>
        public static ErrorStrategy ContinueWithTimeLimit(TimeSpan maxTime) =>
            new TimeLimitImpl(maxTime, null);

        internal class Invariant : ErrorStrategy
        {
            private readonly Action _action;

            internal Invariant(Action action) { _action = action; }

            public override Result Apply(Exception _) =>
                new Result { Action = _action };
        }

        internal class MaxAttemptsImpl : ErrorStrategy
        {
            private readonly int _maxAttempts;
            private readonly int _counter;

            internal MaxAttemptsImpl(int maxAttempts, int counter)
            {
                _maxAttempts = maxAttempts;
                _counter = counter;
            }

            public override Result Apply(Exception _) =>
                _counter < _maxAttempts ?
                    new Result { Action = Action.Continue, Next = new MaxAttemptsImpl(_maxAttempts, _counter + 1) } :
                    new Result { Action = Action.Throw };
        }

        internal class TimeLimitImpl : ErrorStrategy
        {
            private readonly TimeSpan _maxTime;
            private readonly DateTime? _startTime;

            internal TimeLimitImpl(TimeSpan maxTime, DateTime? startTime)
            {
                _maxTime = maxTime;
                _startTime = startTime;
            }

            public override Result Apply(Exception _)
            {
                if (!_startTime.HasValue)
                {
                    return new Result
                    {
                        Action = Action.Continue,
                        Next = new TimeLimitImpl(_maxTime, DateTime.Now)
                    };
                }
                return new Result
                {
                    Action = _startTime.Value.Add(_maxTime) > DateTime.Now ?
                        Action.Continue : Action.Throw
                };
            }
        }
    }
}
