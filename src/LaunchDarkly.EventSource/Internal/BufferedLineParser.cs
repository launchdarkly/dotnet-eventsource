using System.Threading.Tasks;
using LaunchDarkly.EventSource.Events;
using LaunchDarkly.EventSource.Exceptions;

namespace LaunchDarkly.EventSource.Internal
{
    /// <summary>
    /// Buffers a byte stream and returns it in chunks while scanning for line endings, which
    /// may be any of CR (\r), LF (\n), or CRLF. The SSE specification allows any of these line
    /// endings for any line in the stream.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To use this class, we repeatedly call <see cref="ReadAsync"/> to obtain a piece of data.
    /// Rather than copying this back into a buffer provided by the caller, BufferedLineParser
    /// exposes its own fixed-size buffer directly and marks the portion being read; the caller
    /// is responsible for inspecting this data before the next call to <see cref="ReadAsync"/>.
    /// </para>
    /// <para>
    /// This class is not thread-safe.
    /// </para>
    /// </remarks>
    internal class BufferedLineParser
    {
        // This abstraction is used instead of Stream because EventParser needs to wrap the
        // stream read in its own timeout logic, but ByteArrayLineScanner doesn't need to
        // know about the details of that.
        internal delegate Task<int> ReadFunc(byte[] b, int offset, int size);

        private ReadFunc _readFunc;
        private readonly byte[] _readBuffer;
        private int _readBufferCount, _scanPos, _chunkStart, _chunkEnd;
        private bool _lastCharWasCr;

        internal struct Chunk
        {
            /// <summary>
            /// A reference to the span of parsed data in the buffer. This is only valid
            /// until the next time ReadAsync is called. The span never includes a line
            /// terminator.
            /// </summary>
            public Utf8ByteSpan Span;

            /// <summary>
            /// True if a line terminator followed this span.
            /// </summary>
            public bool EndOfLine;
        }

        public BufferedLineParser(
            ReadFunc readFunc,
            int capacity
            )
        {
            _readFunc = readFunc;
            _readBuffer = new byte[capacity];
            _scanPos = _readBufferCount = 0;
        }

        /// <summary>
        /// Attempts to read the next chunk. A chunk is terminated either by a line ending, or
        /// by reaching the end of the buffer before the next read from the underlying stream.
        /// </summary>
        /// <returns></returns>
        public async Task<Chunk> ReadAsync()
        {
            if (_scanPos > 0 && _readBufferCount > _scanPos)
            {
                // Shift the data left to the start of the buffer to make room
                System.Buffer.BlockCopy(_readBuffer, _scanPos, _readBuffer, 0, _readBufferCount - _scanPos);
            }
            _readBufferCount -= _scanPos;
            _scanPos = _chunkStart = _chunkEnd = 0;
            while (true)
            {
                if (_scanPos < _readBufferCount && ScanForTerminator())
                {
                    return new Chunk { Span = CurrentSpan, EndOfLine = true };
                }
                if (_readBufferCount == _readBuffer.Length)
                {
                    return new Chunk { Span = CurrentSpan, EndOfLine = false };
                }
                if (!await ReadMoreIntoBuffer())
                {
                    throw new StreamClosedByServerException();
                }
            }
        }

        private Utf8ByteSpan CurrentSpan =>
            new Utf8ByteSpan(_readBuffer, _chunkStart, _chunkEnd - _chunkStart);

        private bool ScanForTerminator()
        {
            if (_lastCharWasCr)
            {
                // This handles the case where the previous reads ended in CR, so we couldn't tell
                // at that time whether it was just a plain CR or part of a CRLF. We know that the
                // previous line has ended either way, we just need to ensure that if the next byte
                // is LF, we skip it.
                _lastCharWasCr = false;
                if (_readBuffer[_scanPos] == '\n')
                {
                    _scanPos++;
                    _chunkStart++;
                }
            }

            while (_scanPos < _readBufferCount)
            {
                byte b = _readBuffer[_scanPos];
                if (b == '\n' || b == '\r')
                {
                    break;
                }
                _scanPos++;
            }
            _chunkEnd = _scanPos;
            if (_scanPos == _readBufferCount)
            {
                // We haven't found a terminator yet; we'll need to read more from the stream.
                return false;
            }

            _scanPos++;
            if (_readBuffer[_chunkEnd] == '\r')
            {
                if (_scanPos == _readBufferCount)
                {
                    _lastCharWasCr = true;
                }
                else if (_readBuffer[_scanPos] == '\n')
                {
                    _scanPos++;
                }
            }
            return true;
        }

        private async Task<bool> ReadMoreIntoBuffer()
        {
            int readCount = await _readFunc(_readBuffer, _readBufferCount,
                _readBuffer.Length - _readBufferCount);
            if (readCount <= 0)
            {
                return false; // stream was closed
            }
            _readBufferCount += readCount;
            return true;
        }
    }
}
