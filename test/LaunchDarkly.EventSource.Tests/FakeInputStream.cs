using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaunchDarkly.EventSource
{
    public class FakeInputStream : Stream
    {
        private byte[][] _chunks;
        private int _curChunk = 0;
        private int _posInChunk = 0;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotImplementedException();
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Logging.Logger Logger;

        public FakeInputStream(params byte[][] chunks)
        {
            _chunks = chunks;
        }

        public FakeInputStream(params string[] chunks) : this(
            chunks.Select(s => Encoding.UTF8.GetBytes(s)).ToArray()) { }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Logger?.Debug("cur={0}, chunks.Length={1}", _curChunk, _chunks.Length);
            if (_curChunk >= _chunks.Length)
            {
                return -1;
            }
            int remaining = _chunks[_curChunk].Length - _posInChunk;
            if (remaining <= count)
            {
                System.Buffer.BlockCopy(_chunks[_curChunk], _posInChunk, buffer, offset, remaining);
                _curChunk++;
                _posInChunk = 0;
                Logger?.Debug("read {0} bytes", remaining);
                return remaining;
            }
            System.Buffer.BlockCopy(_chunks[_curChunk], _posInChunk, buffer, offset, count);
            _posInChunk += count;
            Logger?.Debug("read {0} bytes", count);
            return count;
        }

        public new Task<int> ReadAsync(byte[] buffer, int offset, int count)
        {
            return Task.FromResult(Read(buffer, offset, count));
        }

        public override long Seek(long offset, SeekOrigin origin) { return 0; }
        public override void SetLength(long value) { }
        public override void Write(byte[] buffer, int offset, int count) { }
    }
}
