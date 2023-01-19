using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.EventSource.Exceptions;
using Xunit;

namespace LaunchDarkly.EventSource
{
    public class ErrorStrategyTest
    {
        private static readonly StreamException _exception = new StreamException();

        [Fact]
        public void AlwaysThrow()
        {
            var result = ErrorStrategy.AlwaysThrow.Apply(_exception);
            Assert.Equal(ErrorStrategy.Action.Throw, result.Action);
            Assert.Null(result.Next);
        }

        [Fact]
        public void AlwaysContinue()
        {
            var result = ErrorStrategy.AlwaysContinue.Apply(_exception);
            Assert.Equal(ErrorStrategy.Action.Continue, result.Action);
            Assert.Null(result.Next);
        }

        [Fact]
        public void MaxAttempts()
        {
            int max = 3;
            var strategy = ErrorStrategy.ContinueWithMaxAttempts(max);
            for (var i = 0; i < max; i++)
            {
                var result = strategy.Apply(_exception);
                Assert.Equal(ErrorStrategy.Action.Continue, result.Action);
                strategy = result.Next ?? strategy;
            }
            Assert.Equal(ErrorStrategy.Action.Throw, strategy.Apply(_exception).Action);
        }

        [Fact]
        public void MaxTime()
        {
            TimeSpan maxTime = TimeSpan.FromMilliseconds(50);
            var strategy = ErrorStrategy.ContinueWithTimeLimit(maxTime);

            var result = strategy.Apply(_exception);
            Assert.Equal(ErrorStrategy.Action.Continue, result.Action);
            strategy = result.Next ?? strategy;

            result = strategy.Apply(_exception);
            Assert.Equal(ErrorStrategy.Action.Continue, result.Action);
            strategy = result.Next ?? strategy;

            Thread.Sleep(maxTime.Add(TimeSpan.FromMilliseconds(1)));

            result = strategy.Apply(_exception);
            Assert.Equal(ErrorStrategy.Action.Throw, result.Action);
            strategy = result.Next ?? strategy;

            result = strategy.Apply(_exception);
            Assert.Equal(ErrorStrategy.Action.Throw, result.Action);
        }
    }
}
