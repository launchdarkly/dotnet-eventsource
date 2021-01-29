using System;
using System.Text;
using Xunit;

namespace LaunchDarkly.EventSource.Tests
{
    public class ByteArrayLineScannerTest
    {
        [Fact]
        public void EmptyBufferReturnsFalse()
        {
            var ls = new ByteArrayLineScanner(100);
            Assert.False(ls.ScanToEndOfLine(out _));
        }

        [Fact]
        public void ScannerParsesLinesWithLF()
        {
            var ls = new ByteArrayLineScanner(100);

            AddBytes(ref ls, "first line");
            Assert.False(ls.ScanToEndOfLine(out _));

            AddBytes(ref ls, " is good\nsecond");
            Assert.True(ls.ScanToEndOfLine(out var line1));
            Assert.Equal("first line is good", line1.GetString());

            AddBytes(ref ls, " line is better\nthird line's the charm\n");
            Assert.True(ls.ScanToEndOfLine(out var line2));
            Assert.Equal("second line is better", line2.GetString());
            Assert.True(ls.ScanToEndOfLine(out var line3));
            Assert.Equal("third line's the charm", line3.GetString());

            // The last character in the buffer was a line ending, so it should have reset the buffer
            Assert.Equal(0, ls.Count);
            Assert.Equal(ls.Capacity, ls.Available);
        }

        [Fact]
        public void ScannerParsesLinesWithCRLF()
        {
            var ls = new ByteArrayLineScanner(100);

            AddBytes(ref ls, "first line");
            Assert.False(ls.ScanToEndOfLine(out _));

            AddBytes(ref ls, " is good\r\nsecond");
            Assert.True(ls.ScanToEndOfLine(out var line1));
            Assert.Equal("first line is good", line1.GetString());

            AddBytes(ref ls, " line is better\r\nthird line's the charm\r\n");
            Assert.True(ls.ScanToEndOfLine(out var line2));
            Assert.Equal("second line is better", line2.GetString());
            Assert.True(ls.ScanToEndOfLine(out var line3));
            Assert.Equal("third line's the charm", line3.GetString());

            // The last character in the buffer was a line ending, so it should have reset the buffer
            Assert.Equal(0, ls.Count);
            Assert.Equal(ls.Capacity, ls.Available);
        }

        [Fact]
        public void ScannerParsesLinesWithCR()
        {
            var ls = new ByteArrayLineScanner(100);

            AddBytes(ref ls, "first line");
            Assert.False(ls.ScanToEndOfLine(out _));

            AddBytes(ref ls, " is good\rsecond");
            Assert.True(ls.ScanToEndOfLine(out var line1));
            Assert.Equal("first line is good", line1.GetString());

            AddBytes(ref ls, " line is better\rthird line's the charm\r");
            Assert.True(ls.ScanToEndOfLine(out var line2));
            Assert.Equal("second line is better", line2.GetString());

            // The last character in the buffer was a CR, so it can't say the line is done till it sees another byte.
            Assert.False(ls.ScanToEndOfLine(out var _));

            AddBytes(ref ls, "x");
            Assert.True(ls.ScanToEndOfLine(out var line3));
            Assert.Equal("third line's the charm", line3.GetString());

            // The last character in the buffer was not a line ending, so it should not have reset the buffer
            Assert.NotEqual(0, ls.Count);
            Assert.NotEqual(ls.Capacity, ls.Available);
        }

        [Fact]
        public void ScannerAccumulatesPartialLines()
        {
            var ls = new ByteArrayLineScanner(10); // deliberately small

            AddBytes(ref ls, "012345");
            Assert.False(ls.ScanToEndOfLine(out _));
            Assert.Equal(6, ls.Count);
            Assert.Equal(4, ls.Available);

            AddBytes(ref ls, "6789");
            Assert.False(ls.ScanToEndOfLine(out _));
            Assert.Equal(0, ls.Count); // it moved the data elsewhere and cleared the buffer to make room for more
            Assert.Equal(10, ls.Available);

            AddBytes(ref ls, "abcd");
            Assert.False(ls.ScanToEndOfLine(out _));

            AddBytes(ref ls, "ef\ngh");
            Assert.True(ls.ScanToEndOfLine(out var line1));
            Assert.Equal("0123456789abcdef", line1.GetString());
            Assert.Equal(2, ls.Count);
            Assert.Equal(8, ls.Available);

            AddBytes(ref ls, "\n");
            Assert.True(ls.ScanToEndOfLine(out var line2));
            Assert.Equal("gh", line2.GetString());
        }

        private static void AddBytes(ref ByteArrayLineScanner ls, string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            Buffer.BlockCopy(bytes, 0, ls.Buffer, ls.Count, bytes.Length);
            ls.AddedBytes(bytes.Length);
        }
    }
}
