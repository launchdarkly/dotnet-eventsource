using System;
using System.IO;
using System.Text;
using LaunchDarkly.TestHelpers;
using Xunit;

namespace LaunchDarkly.EventSource.Events
{
    public class MessageEventTest
    {
        private static readonly Uri Uri1 = new Uri("http://uri1"),
            Uri2 = new Uri("http://uri2");

        [Fact]
        public void BasicProperties()
        {
            var e1 = new MessageEvent("name1", "data1", "id1", Uri1);
            Assert.Equal("name1", e1.Name);
            Assert.Equal("data1", e1.Data);
            Assert.Equal("id1", e1.LastEventId);
            Assert.Equal(Uri1, e1.Origin);

            var e2 = new MessageEvent("name1", "data1", Uri2);
            Assert.Equal("name1", e2.Name);
            Assert.Equal("data1", e2.Data);
            Assert.Null(e2.LastEventId);
            Assert.Equal(Uri2, e2.Origin);
        }

        [Fact]
        public void StreamingDataBehavior()
        {
            var data = "lazily-read-data";

            var e1 = new MessageEvent(
                "name",
                new MemoryStream(Encoding.UTF8.GetBytes(data)),
                "id",
                Uri1);
            Assert.Equal("name", e1.Name);
            Assert.Equal("id", e1.LastEventId);
            Assert.Equal(Uri1, e1.Origin);
            Assert.Equal(data, new StreamReader(e1.DataStream, Encoding.UTF8).ReadToEnd());

            var e2 = new MessageEvent(
                "name",
                new MemoryStream(Encoding.UTF8.GetBytes(data)),
                "id",
                Uri1);
            Assert.Equal("name", e2.Name);
            Assert.Equal("id", e2.LastEventId);
            Assert.Equal(Uri1, e2.Origin);
            Assert.Equal(data, e2.Data);
            Assert.Equal(data, e2.Data); // can read twice, it's memoized

            var e3 = new MessageEvent(
                "name",
                data, // passed in as a string rather than a stream
                "id",
                Uri1);
            // can read the string as a stream
            Assert.Equal(data, new StreamReader(e3.DataStream, Encoding.UTF8).ReadToEnd());
        }

        [Fact]
        public void EqualityAndHashCode()
        {
            TypeBehavior.CheckEqualsAndHashCode(
                () => new MessageEvent("name1", "data1", "id1", Uri1),
                () => new MessageEvent("name2", "data1", "id1", Uri1),
                () => new MessageEvent("name1", "data2", "id1", Uri1),
                () => new MessageEvent("name1", "data1", "id2", Uri1),
                () => new MessageEvent("name1", "data1", "id1", Uri2),
                () => new MessageEvent("name1", "data1", null, Uri1),
                () => new MessageEvent("name1", "data1", "id1", null)
                );
        }
    }
}
