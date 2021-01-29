using System;
using System.IO;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// Internal implementation of a buffered text line parser for UTF-8 data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is used as follows. 1. The caller puts some data into the byte buffer. 2. The caller
    /// calls ScanToEndOfLine; if it returns true, the <c>lineOut</c> parameter is set to point
    /// to the content of the line (not including the line ending character(s)). If it returns
    /// false, put more data into the buffer and try again.
    /// </para>
    /// <para>
    /// Since in UTF-8 all multi-byte characters use values greater than 127 for all of their
    /// bytes, this logic doesn't need to do any UTF-8 decoding or even know how many bytes are
    /// in a character; it just looks for the line-ending sequences CR, LF, or CR+LF.
    /// </para>
    /// </remarks>
    internal struct ByteArrayLineScanner
    {
        private readonly byte[] _buffer;
        private readonly int _capacity;
        private int _count;
        private int _startPos;

        public byte[] Buffer => _buffer;
        public int Capacity => _capacity;
        public int Count => _count;
        public int Available => _capacity - _count;

        private MemoryStream _partialLine;

        public ByteArrayLineScanner(int capacity)
        {
            _buffer = new byte[capacity];
            _capacity = capacity;
            _count = 0;
            _partialLine = null;
            _startPos = 0;
        }

        /// <summary>
        /// The caller calls this method after having already added <c>count</c> more bytes at the
        /// end of the buffer. We do it this way instead of having an <c>AddBytes(byte[], int)</c>
        /// method because we don't want to allocate a second buffer just to be the destination for
        /// a read operation.
        /// </summary>
        /// <param name="count">number of bytes added</param>
        public void AddedBytes(int count)
        {
            _count += count;
        }

        /// <summary>
        /// Searches for the next line ending and, if successful, provides the line data.
        /// </summary>
        /// <param name="lineOut">if successful, this is set to point to the bytes for the line
        /// <i>not</i> including any CR/LF; whenever possible this is a reference to the underlying
        /// buffer, not a copy, so the caller should read/copy it before doing anything else to the
        /// buffer</param>
        /// <returns>true if a full line was read, false if we need more data first</returns>
        public bool ScanToEndOfLine(out Utf8ByteSpan lineOut)
        {
            if (_startPos == _count)
            {
                _startPos = _count = 0;
                lineOut = new Utf8ByteSpan();
                return false;
            }

            if (_startPos == 0 && _partialLine != null && _partialLine.Position > 0
                && _partialLine.GetBuffer()[_partialLine.Position - 1] == '\r')
            {
                // This is an edge case where the very last byte we previously saw was a CR, and we didn't know
                // whether the next byte would be LF or not, but we had to dump the buffer into _partialLine
                // because it was completely full. So, now we can return the line that's already in _partialLine,
                // but if the first byte in the buffer is LF we should skip past it.
                if (_buffer[_startPos] == '\n')
                {
                    _startPos++;
                }
                lineOut = new Utf8ByteSpan(_partialLine.GetBuffer(), 0,
                    (int)_partialLine.Position - 1); // don't include the CR
                _partialLine = null;
                return true;
            }

            int startedAt = _startPos, pos = _startPos;

            while (pos < _count)
            {
                var b = _buffer[pos];
                if (b == '\n') // LF by itself terminates a line
                {
                    _startPos = pos + 1; // next line will start after the LF
                    break;
                }
                if (b == '\r')
                {
                    if (pos < (_count - 1))
                    {
                        _startPos = pos + 1; // next line will start after the CR--
                        if (_buffer[pos + 1] == '\n') // --unless there was an LF right after that
                        {
                            _startPos++;
                        }
                        break;
                    }
                    else
                    {
                        // CR by itself and CR+LF are both valid line endings in SSE, so if the very
                        // last character we saw was a CR, we can't know when the line is fully read
                        // until we've gotten more data. So we'll need to treat this as an incomplete
                        // line.
                        pos++;
                        break;
                    }
                }
                pos++;
            }

            if (pos == _count) // we didn't find a line terminator
            {
                lineOut = new Utf8ByteSpan();
                if (_count < _capacity)
                {
                    // There's still room in the buffer, so we'll re-scan the line once they add more bytes
                    return false;
                }
                // We need to dump the incomplete line into _partialLine so we can make room in the buffer
                var partialCount = pos - _startPos;
                if (_partialLine is null)
                {
                    _partialLine = new MemoryStream(partialCount);
                }
                _partialLine.Write(_buffer, _startPos, partialCount);

                // Clear the main buffer
                _startPos = _count = 0;

                return false;
            }

            if (_partialLine != null && _partialLine.Position > 0)
            {
                _partialLine.Write(_buffer, startedAt, pos - startedAt);
                lineOut = new Utf8ByteSpan(_partialLine.GetBuffer(), 0, (int)_partialLine.Position);
                _partialLine = null;

                // If there are still bytes in the main buffer, move them over to make more room. It's
                // safe for us to do this before the caller has looked at lineOut, because lineOut is now
                // a reference to the separate _partialLine buffer, not to the main buffer.
                if (_startPos < _count)
                {
                    System.Buffer.BlockCopy(_buffer, _startPos, _buffer, 0, _count - _startPos);
                }
                _count -= _startPos;
                _startPos = 0;
            }
            else
            {
                lineOut = new Utf8ByteSpan(_buffer, startedAt, pos - startedAt);
                if (_startPos == _count)
                {
                    // If we've scanned all the data in the buffer, reset _startPos and _count to indicate
                    // that the entire buffer is available for the next read. It's safe for us to do this
                    // before the caller has looked at lineOut, because we're not actually modifying any
                    // bytes in the buffer. It's the caller's not responsibility not to modify the buffer
                    // until it has already done whatever needs to be done with the lineOut data.
                    _startPos = _count = 0;
                }
            }
            return true;
        }
    }
}
