using System;
using System.Text;

namespace LaunchDarkly.EventSource.Internal
{
    /// <summary>
    /// Points to a span of UTF-8-encoded text in a buffer.
    /// </summary>
    /// <remarks>
    /// This is similar to the <c>Span</c> type in .NET 5, which we can't use because
    /// we must support older target frameworks. It is used internally by
    /// <c>EventSource</c> to pass references to internal buffers.
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
        /// Converts the UTF-8 byte data to a string.
        /// </summary>
        /// <returns>A new string.</returns>
        public string GetString() => Data is null ? "" : Encoding.UTF8.GetString(Data, Offset, Length);
    }
}
