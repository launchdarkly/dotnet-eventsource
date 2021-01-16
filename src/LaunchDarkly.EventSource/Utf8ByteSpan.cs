using System;
using System.Text;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// Points to a span of UTF-8-encoded text in a buffer.
    /// </summary>
    /// <remarks>
    /// This is similar to the <c>Span</c> type in .NET 5. It is used internally by
    /// <c>EventSource</c> to store event data if the stream is using UTF-8 encoding.
    /// If so, reading <see cref="MessageEvent.DataUtf8Bytes"/> will return the same
    /// byte array, to avoid unnecessary copying.
    /// </remarks>
    public struct Utf8ByteSpan
    {
        /// <summary>
        /// The byte array containing the data. May be null if <see cref="Length"/> is zero.
        /// </summary>
        /// <remarks>
        /// It is the caller's responsibility not to modify the array.
        /// </remarks>
        public byte[] Data { get; }

        /// <summary>
        /// The offset of the first relevant byte of data within the array. This may be
        /// greater than zero if the span represents a subset of a larger buffer.
        /// </summary>
        public int Offset { get; }

        /// <summary>
        /// The number of bytes of relevant data within the array. This may be less than
        /// <c>Data.Length</c> if the span represents a subset of a larger buffer.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="data">The byte array containing the data.</param>
        /// <param name="offset">The offset of the first relevant byte of data within the array.</param>
        /// <param name="length">The number of bytes of relevant data within the array.</param>
        public Utf8ByteSpan(byte[] data, int offset, int length)
        {
            Data = length == 0 ? null : data;
            Offset = offset;
            Length = length;
        }

        /// <summary>
        /// Constructs a new instance by copying a string.
        /// </summary>
        /// <param name="s">A string to convert to UTF-8 bytes.</param>
        public Utf8ByteSpan(string s)
        {
            Data = s.Length == 0 ? null : Encoding.UTF8.GetBytes(s);
            Offset = 0;
            Length = Data is null ? 0 : Data.Length;
        }

        /// <summary>
        /// Converts the UTF-8 byte data to a string.
        /// </summary>
        /// <returns>A new string.</returns>
        public string GetString() => Data is null ? "" : Encoding.UTF8.GetString(Data, Offset, Length);

        /// <summary>
        /// Tests whether the bytes in this span are the same as another span.
        /// </summary>
        /// <param name="other">Another <c>Utf8ByteSpan</c>.</param>
        /// <returns>True if the two spans have the same length and the same
        /// data, starting from each one's <c>Offset</c>.</returns>
        public bool Equals(Utf8ByteSpan other)
        {
            var len = Length;
            if (len != other.Length)
            {
                return false;
            }
#if NETSTANDARD2_1 || NETCOREAPP2_1 || NET5_0
            return AsSpan.SequenceEqual(other.AsSpan);
#else
            int offset = Offset, otherOffset = other.Offset;
            byte[] data = Data, otherData = other.Data;
            for (int i = 0; i < len; i++)
            {
                if (data[i + offset] != otherData[i + otherOffset])
                {
                    return false;
                }
            }
            return true;
#endif
        }

#if NETSTANDARD2_1 || NETCOREAPP2_1 || NET5_0
        private Span<byte> AsSpan => new Span<byte>(Data, Offset, Length);
#endif

        /// <summary>
        /// Tests whether the bytes in this span are the same as the UTF-8 encoding
        /// of the specified string.
        /// </summary>
        /// <remarks>
        /// This method is potentially inefficient and should be used only in testing.
        /// </remarks>
        /// <param name="s">A string.</param>
        /// <returns>True if the bytes are equivalent.</returns>
        public bool Equals(string s)
        {
            byte[] data = Data;
            int offset = Offset, len = Length;
            for (int i = 0; i < len; i++)
            {
                if (data[offset + i] > 127)
                {
                    // There are multi-byte characters in the span, so we can't do a 1:1 comparison of bytes
                    return GetString().Equals(s);
                }
            }
            // There are no multi-byte characters, so we don't need to convert to or from a UTF-16 string
            if (len != s.Length)
            {
                return false;
            }
            for (int i = 0; i < len; i++)
            {
                if ((char)data[offset + i] != s[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
