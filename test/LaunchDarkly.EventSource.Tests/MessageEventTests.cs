using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace LaunchDarkly.EventSource.Tests
{
    public class MessageEventTests
    {
        [Theory]
        [InlineData("http://test.com", null, null)]
        [InlineData("http://test.com", "testing", null)]
        [InlineData("http://test1.com", "something", "200")]
        [InlineData("http://test2.com", "various", "125")]
        [InlineData("http://test3.com", "testing", "1")]
        public void Two_identical_message_events_are_equal(string url, string data, string lastEventId)
        {
            var uri = new Uri(url);

            MessageEvent event1 = new MessageEvent(data, lastEventId, uri);

            MessageEvent event2 = new MessageEvent(data, lastEventId, uri);

            Assert.Equal(event1, event2);
        }
 

        [Fact]
        public void Two_Message_events_are_not_equal()
        {
            var uri = new Uri("http://test.com");

            MessageEvent event1 = new MessageEvent("Event 1", uri);

            MessageEvent event2 = new MessageEvent("Event 2", uri);

            Assert.NotEqual(event1, event2);
        }

        [Theory]
        [InlineData("http://test.com", null, null)]
        [InlineData("http://test.com", "testing", null)]
        [InlineData("http://test1.com", "something", "200")]
        [InlineData("http://test2.com", "various", "125")]
        [InlineData("http://test3.com", "testing", "1")]
        public void Message_event_hashcode_returns_the_same_value_when_called_twice(string url, string data, string lastEventId)
        {

            MessageEvent event1 = new MessageEvent(data, lastEventId, new Uri(url));

            var hash1 = event1.GetHashCode();

            var hash2 = event1.GetHashCode();

            Assert.Equal(hash1, hash2);

        }


        [Fact]
        public void Message_event_hashcode_returns_different_values_when_property_values_changed()
        {
            var uri = new Uri("http://test.com");

            MessageEvent event1 = new MessageEvent("test", uri);

            var hash1 = event1.GetHashCode();

            var event2 = new MessageEvent("test2", uri);

            var hash2 = event2.GetHashCode();

            Assert.NotEqual(hash1, hash2);

        }
    }
}
