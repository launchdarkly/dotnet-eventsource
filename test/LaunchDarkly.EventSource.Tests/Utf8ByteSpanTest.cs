using System;
using System.Text;
using Xunit;

namespace LaunchDarkly.EventSource.Tests
{
    public class Utf8ByteSpanTest
    {
        [Fact]
        public void ConstructorSetsSpanProperties()
        {
            var buf = new byte[100];
            var span = new Utf8ByteSpan(buf, 1, 5);
            Assert.Same(buf, span.Data);
            Assert.Equal(1, span.Offset);
            Assert.Equal(5, span.Length);
        }

        [Fact]
        public void ConstructorFromString()
        {
            var s = "olé";
            var bytes = Encoding.UTF8.GetBytes(s);
            var span = new Utf8ByteSpan(s);
            Assert.Equal(s, Encoding.UTF8.GetString(span.Data));
            Assert.Equal(0, span.Offset);
            Assert.Equal(bytes.Length, span.Length);
        }

        [Fact]
        public void ZeroLengthSpanDoesNotStoreDataBuffer()
        {
            var span = new Utf8ByteSpan(new byte[100], 1, 0);
            Assert.Equal(0, span.Length);
            Assert.Null(span.Data);
        }

        [Fact]
        public void ZeroLengthSpanFromEmptyStringDoesNotStoreDataBuffer()
        {
            var span = new Utf8ByteSpan("");
            Assert.Equal(0, span.Length);
            Assert.Null(span.Data);
        }

        [Fact]
        public void SpanToString()
        {
            var s = "olé";
            var bytes = Encoding.UTF8.GetBytes(s);
            var span = new Utf8ByteSpan(bytes, 0, bytes.Length);
            Assert.Equal(s, span.GetString());
        }

        [Fact]
        public void SpanWithOffsetToString()
        {
            var s = "olé";
            var bytes = Encoding.UTF8.GetBytes("zz" + s + "!");
            var span = new Utf8ByteSpan(bytes, 2, bytes.Length - 3);
            Assert.Equal(s, span.GetString());
        }

        [Fact]
        public void SpanEqualsSpan()
        {
            var span1 = new Utf8ByteSpan("abc");
            var span2 = new Utf8ByteSpan("abc");
            Assert.True(span1.Equals(span2));

            var span3 = new Utf8ByteSpan(Encoding.UTF8.GetBytes("zzabc!"), 2, 3);
            Assert.True(span1.Equals(span3));

            var span4 = new Utf8ByteSpan("abd");
            Assert.False(span1.Equals(span4));
        }

        [Fact]
        public void SpanEqualsStringWithMultiByteCharacters()
        {
            var s = "olé";
            var bytes = Encoding.UTF8.GetBytes("zz" + s + "!");

            var span1 = new Utf8ByteSpan(s);
            Assert.True(span1.Equals(s));

            var span2 = new Utf8ByteSpan(bytes, 2, bytes.Length - 3);
            Assert.True(span2.Equals(s));

            var span3 = new Utf8ByteSpan("olè");
            Assert.False(span3.Equals(s));
        }

        [Fact]
        public void SpanEqualsStringWithSingleByteCharacters()
        {
            var s = "abc";
            var bytes = Encoding.UTF8.GetBytes("zz" + s + "!");

            var span1 = new Utf8ByteSpan(s);
            Assert.True(span1.Equals(s));

            var span2 = new Utf8ByteSpan(bytes, 2, bytes.Length - 3);
            Assert.True(span2.Equals(s));

            var span3 = new Utf8ByteSpan("abd");
            Assert.False(span3.Equals(s));
        }
    }
}
