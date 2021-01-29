using System;
using Xunit;

namespace LaunchDarkly.EventSource.Tests
{
    public class MessageEventTest
    {
        [Theory]
        [InlineData("http://test.com", null, null)]
        [InlineData("http://test.com", "testing", null)]
        [InlineData("http://test1.com", "something", "200")]
        [InlineData("http://test2.com", "various", "125")]
        [InlineData("http://test3.com", "testing", "1")]
        public void MessageEventEqual(string url, string data, string lastEventId)
        {
            var uri = new Uri(url);

            MessageEvent event1 = new MessageEvent("message", data, lastEventId, uri);

            MessageEvent event2 = new MessageEvent("message", data, lastEventId, uri);

            Assert.Equal(event1, event2);
        }
 
        [Fact]
        public void MessageEventNotEqual()
        {
            var uri = new Uri("http://test.com");

            Assert.NotEqual(new MessageEvent("name1", "data", "id", uri),
                new MessageEvent("name2", "data", "id", uri));
            Assert.NotEqual(new MessageEvent("name", "data1", "id", uri),
                new MessageEvent("name", "data2", "id", uri));
            Assert.NotEqual(new MessageEvent("name", "data", "id1", uri),
                new MessageEvent("name", "data", "id2", uri));
            Assert.NotEqual(new MessageEvent("name", "data", "id", uri),
                new MessageEvent("name", "data", "id", new Uri("http://other")));
        }

        [Theory]
        [InlineData("http://test.com", null, null)]
        [InlineData("http://test.com", "testing", null)]
        [InlineData("http://test1.com", "something", "200")]
        [InlineData("http://test2.com", "various", "125")]
        [InlineData("http://test3.com", "testing", "1")]
        public void Message_event_hashcode_returns_the_same_value_when_called_twice(string url, string data, string lastEventId)
        {

            MessageEvent event1 = new MessageEvent("message", data, lastEventId, new Uri(url));

            var hash1 = event1.GetHashCode();

            var hash2 = event1.GetHashCode();

            Assert.Equal(hash1, hash2);

        }


        [Fact]
        public void Message_event_hashcode_returns_different_values_when_property_values_changed()
        {
            var uri = new Uri("http://test.com");

            MessageEvent event1 = new MessageEvent("message", "test", uri);

            var hash1 = event1.GetHashCode();

            var event2 = new MessageEvent("message", "test2", uri);

            var hash2 = event2.GetHashCode();

            Assert.NotEqual(hash1, hash2);

        }
    }
}
