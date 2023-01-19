using System.Text;
using Xunit;

namespace LaunchDarkly.EventSource.Internal
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
        public void ZeroLengthSpanDoesNotStoreDataBuffer()
        {
            var span = new Utf8ByteSpan(new byte[100], 1, 0);
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
    }
}
