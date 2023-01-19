using System;
using LaunchDarkly.EventSource.Exceptions;
using LaunchDarkly.TestHelpers;
using Xunit;

namespace LaunchDarkly.EventSource.Events
{
    public class EventTypesTest
    {
        [Fact]
        public void TestCommentEvent()
        {
            Assert.Equal("a", new CommentEvent("a").Text);

            TypeBehavior.CheckEqualsAndHashCode(
                () => new CommentEvent("a"),
                () => new CommentEvent("b")
                );
        }

        [Fact]
        public void TestFaultEvent()
        {
            var ex = new ReadTimeoutException();
            Assert.Same(ex, new FaultEvent(ex).Exception);

            TypeBehavior.CheckEqualsAndHashCode(
                () => new FaultEvent(new ReadTimeoutException()),
                () => new FaultEvent(new StreamClosedByCallerException())
                );
        }

        [Fact]
        public void TestStartedEvent()
        {
            TypeBehavior.CheckEqualsAndHashCode(
                () => new StartedEvent()
                );
        }
    }
}
