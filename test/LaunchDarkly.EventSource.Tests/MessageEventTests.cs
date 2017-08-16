using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace LaunchDarkly.EventSource.Tests
{
    public class MessageEventTests
    {
        [Fact]
        public void Two_identical_message_events_are_equal()
        {
            var uri = new Uri("http://test.com");

            MessageEvent event1 = new MessageEvent("test", uri);

            MessageEvent event2 = new MessageEvent("test", uri);

            Assert.Equal(event1, event2);
        }

        [Fact]
        public void Two_Message_events_are_different()
        {
            var uri = new Uri("http://test.com");

            MessageEvent event1 = new MessageEvent("test", uri);

            MessageEvent event2 = new MessageEvent("test2", uri);

            Assert.NotEqual(event1, event2);
        }

        [Fact]
        public void Message_event_hashcode_returns_the_same_value_when_called_twice()
        {

            MessageEvent event1 = new MessageEvent("test", new Uri("http://test.com"));

            var hash1 = event1.GetHashCode();

            var hash2 = event1.GetHashCode();

            Assert.Equal(hash1, hash2);

        }
    }
}
