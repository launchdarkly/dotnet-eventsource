﻿using System;
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