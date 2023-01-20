using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.EventSource.Events;
using LaunchDarkly.EventSource.Exceptions;
using LaunchDarkly.Logging;

using static LaunchDarkly.EventSource.Internal.AsyncHelpers;

namespace LaunchDarkly.EventSource.Internal
{
    /// <summary>
    /// An internal class containing helper methods to parse Server Sent Event data.
    /// </summary>
    internal sealed class EventParser
    {
        internal const int ValueBufferInitialCapacity = 1000;

        private readonly Stream _stream;
        private readonly BufferedLineParser _lineParser;
        private readonly TimeSpan _readTimeout;
        private readonly Uri _origin;
        private readonly bool _streamEventData;
        private readonly HashSet<string> _expectFields;
        private readonly CancellationToken _cancellationToken;
        private readonly Logger _logger;

        private Utf8ByteSpan _chunk;
        private bool _lineEnded;
        private int _currentLineLength;

        private MemoryStream _dataBuffer;  // accumulates "data" lines
        private MemoryStream _valueBuffer; // used whenever a field other than "data" has a value longer than one chunk

        private bool _haveData;       // true if we have seen at least one "data" line so far in this event
        private bool _dataLineEnded;  // true if the previous chunk of "data" ended in a line terminator
        private string _fieldName;    // name of the field we are currently parsing (might be spread across multiple chunks)
        private string _eventName;    // value of "event:" field in this event, if any
        private string _lastEventId;  // value of "id:" field in this event, if any
        private bool _skipRestOfLine; // true if we are skipping over an invalid line
        private bool _skipRestOfMessage; // true if we are skipping over an invalid line
        private IncrementalMessageDataInputStream _currentMessageDataStream;

        public EventParser(
            Stream stream,
            int readBufferSize,
            TimeSpan readTimeout,
            Uri origin,
            bool streamEventData,
            HashSet<string> expectFields,
            CancellationToken cancellationToken,
            Logger logger
            )
        {
            _stream = stream;
            _lineParser = new BufferedLineParser(
                ReadFromStream,
                readBufferSize
                );
            _readTimeout = readTimeout;
            _origin = origin;
            _streamEventData = streamEventData;
            _expectFields = expectFields is null ? new HashSet<string>() :
                new HashSet<string>(expectFields); // copy for immutability
            _cancellationToken = cancellationToken;
            _logger = logger;

            _dataBuffer = new MemoryStream(ValueBufferInitialCapacity);
        }

        /// <summary>
        /// Synchronously obtains the next event from the stream-- either parsing
        /// already-read data from the read buffer, or reading more data if necessary,
        /// until an event is available.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method always either returns an event or throws an exception. If it
        /// throws an exception, the stream should be considered invalid/closed.
        /// </para>
        /// <para>
        /// The return value is always a MessageEvent, a CommentEvent, or a
        /// SetRetryDelayEvent. StartedEvent and FaultEvent are not returned by this
        /// method because they are by-products of state changes in EventSource.
        /// </para>
        /// </remarks>
        /// <returns>the next event</returns>
        public async Task<IEvent> NextEventAsync()
        {
            while (true)
            {
                var e = await TryNextEventAsync();
                if (e != null)
                {
                    return e;
                }
            }
        }

        // This inner method exists just to simplify control flow: whenever we need to
        // obtain some more data before continuing, we can just return null so NextEvent
        // will call us again.
        private async Task<IEvent> TryNextEventAsync()
        {
            if (_currentMessageDataStream != null)
            {
                // We dispatched an incremental message that has not yet been fully read, so we need
                // to skip the rest of that message before we can proceed.
                _currentMessageDataStream.Close();
            }

            await GetNextChunk(_cancellationToken); // throws exception if stream has closed

            if (_skipRestOfMessage)
            {
                // If we're in this state, it means we want to ignore everything we see until the
                // next blank line.
                if (_lineEnded && _currentLineLength == 0)
                {
                    _skipRestOfMessage = false;
                    ResetState();
                }
                return null; // no event available yet, loop for more data
            }

            if (_skipRestOfLine)
            {
                // If we're in this state, it means we already know we want to ignore this line and
                // not bother buffering or parsing the rest of it - just keep reading till we see a
                // line terminator. We do this if we couldn't find a colon in the first chunk we read,
                // meaning that the field name can't possibly be valid even if there is a colon later.
                _skipRestOfLine = !_lineEnded;
                return null;
            }

            if (_lineEnded && _currentLineLength == 0)
            {
                // Blank line means end of message-- if we're currently reading a message.
                if (!_haveData)
                {
                    ResetState();
                    return null; // no event available yet, loop for more data
                }

                var name = _eventName ?? MessageEvent.DefaultName;
                _eventName = null;
                var dataString = Encoding.UTF8.GetString(_dataBuffer.GetBuffer(), 0, (int)_dataBuffer.Length);
                MessageEvent message = new MessageEvent(name, dataString, _lastEventId, _origin);
                ResetState();
                _logger.Debug("Received event \"{0}\"", message.Name);
                return message;
            }

            if (_fieldName is null)
            { // we haven't yet parsed the field name
                _fieldName = ParseFieldName();
                if (_fieldName is null)
                {
                    // We didn't find a colon. Since the capacity of our line buffer is always greater
                    // than the length of the longest valid SSE field name plus a colon, the chunk that
                    // we have now is either a field name with no value... or, if we haven't yet hit a
                    // line terminator, it could be an extremely long field name that didn't fit in the
                    // buffer, but in that case it is definitely not a real SSE field since those all
                    // have short names, so then we know we can skip the rest of this line.
                    _skipRestOfLine = !_lineEnded;
                    return null; // no event available yet, loop for more data
                }
            }

            if (_fieldName == Constants.DataField)
            {
                // We have not already started streaming data for this event. Should we?
                if (CanStreamEventDataNow())
                {
                    // We are in streaming data mode, so as soon as we see the start of "data:" we
                    // should create a decorator stream and return a message that will read from it.
                    // We won't come back to TryNextEventAsync() until the caller is finished with that
                    // message (or, if they try to read another message before this one has been fully
                    // read, the logic at the top of TryNextEventAsync() will cause this message to be skipped).
                    var messageDataStream = new IncrementalMessageDataInputStream(this);
                    _currentMessageDataStream = messageDataStream;
                    MessageEvent message = new MessageEvent(
                        _eventName ?? MessageEvent.DefaultName,
                        messageDataStream,
                        _lastEventId,
                        _origin
                        );
                    _logger.Debug("Received event \"{0}\" with streaming data", _eventName);
                    return message;
                }

                // Streaming data is not enabled, so we'll accumulate this data in a buffer until
                // we've seen the end of the event.
                if (_dataLineEnded)
                {
                    _dataBuffer.WriteByte((byte)'\n');
                }
                if (_chunk.Length != 0)
                {
                    _dataBuffer.Write(_chunk.Data, _chunk.Offset, _chunk.Length);
                }
                _dataLineEnded = _lineEnded;
                _haveData = true;
                if (_lineEnded)
                {
                    _fieldName = null;
                }
                return null; // no event available yet, loop for more data
            }

            // For any field other than "data:", we never do any kind of streaming shenanigans
            // and there can only be a single line of a value, so we just get the whole value
            // as a string. If the whole line fits into the buffer then we can do this in one
            // step; otherwise we'll accumulate chunks in another buffer until the line is done.
            if (!_lineEnded)
            {
                if (_valueBuffer is null)
                {
                    _valueBuffer = new MemoryStream(ValueBufferInitialCapacity);
                }
                _valueBuffer.Write(_chunk.Data, _chunk.Offset, _chunk.Length);
                return null; // Don't have a full event yet
            }

            var completedFieldName = _fieldName;
            _fieldName = null; // next line will need a field name
            string fieldValue;
            if (_valueBuffer is null || _valueBuffer.Length == 0)
            {
                fieldValue = _chunk.GetString();
            }
            else
            {
                // we had accumulated a partial value in a previous read
                _valueBuffer.Write(_chunk.Data, _chunk.Offset, _chunk.Length);
                fieldValue = Encoding.UTF8.GetString(_valueBuffer.GetBuffer(), 0, (int)_valueBuffer.Length);
                ResetValueBuffer();
            }

            switch (completedFieldName)
            {
                case "":
                    _logger.Debug("Received comment: {0}", fieldValue);
                    return new CommentEvent(fieldValue);
                case Constants.EventField:
                    _eventName = fieldValue;
                    break;
                case Constants.IdField:
                    if (!fieldValue.Contains("\x00")) // per SSE spec, id field cannot contain a null character
                    {
                        _lastEventId = fieldValue;
                    }
                    break;
                case Constants.RetryField:
                    if (long.TryParse(fieldValue, out var millis))
                    {
                        return new SetRetryDelayEvent(TimeSpan.FromMilliseconds(millis));
                    }
                    // ignore any non-numeric value
                    break;
                default:
                    // For an unrecognized field name, we do nothing.
                    break;
            }
            return null;
        }

        private async Task GetNextChunk(CancellationToken cancellationTokenForThisRead)
        {
            var chunk = await _lineParser.ReadAsync(cancellationTokenForThisRead); // throws exception if stream has closed
            _chunk = chunk.Span;
            var previousLineEnded = _lineEnded;
            _lineEnded = chunk.EndOfLine;
            if (previousLineEnded)
            {
                _currentLineLength = 0;
            }
            _currentLineLength += _chunk.Length;
        }

        private string ParseFieldName()
        {
            int offset = _chunk.Offset, length = _chunk.Length;
            int nameLength = 0;
            for (; nameLength < length && _chunk.Data[offset + nameLength] != ':'; nameLength++) { }
            ResetValueBuffer();
            if (nameLength == length && !_lineEnded)
            {
                // The line was longer than the buffer, and we did not find a colon. Since no valid
                // SSE field name would be longer than our buffer, we can consider this line invalid.
                // (But if lineEnded is true, that's OK-- a line consisting of nothing but a field
                // name is OK in SSE-- so we'll fall through below in that case.)
                return null;
            }
            String name = nameLength == 0 ? "" : Encoding.UTF8.GetString(_chunk.Data, offset, nameLength);
            if (nameLength < length)
            {
                nameLength++;
                if (nameLength < length && _chunk.Data[offset + nameLength] == ' ')
                {
                    // Skip exactly one leading space at the start of the value, if any
                    nameLength++;
                }
            }
            _chunk = new Utf8ByteSpan(_chunk.Data, offset + nameLength, length - nameLength);
            return name;
        }

        private bool CanStreamEventDataNow() =>
            _streamEventData &&
            !(_expectFields.Contains(Constants.EventField) && _eventName is null) &&
            !(_expectFields.Contains(Constants.IdField) && _lastEventId == null);

        private void ResetState()
        {
            _haveData = _dataLineEnded = false;
            _eventName = _fieldName = null;
            ResetValueBuffer();
            if (_dataBuffer.Length != 0)
            {
                if (_dataBuffer.Length > ValueBufferInitialCapacity)
                {
                    _dataBuffer = null; // don't want it to grow indefinitely
                }
                else
                {
                    _dataBuffer.SetLength(0);
                }
            }
        }

        private void ResetValueBuffer()
        {
            if (_valueBuffer != null)
            {
                if (_valueBuffer.Length > ValueBufferInitialCapacity)
                {
                    _valueBuffer = null; // don't want it to grow indefinitely, and might not ever need it again
                }
                else
                {
                    _valueBuffer.SetLength(0);
                }
            }
        }

        private Task<int> ReadFromStream(byte[] b, int offset, int size,
            CancellationToken cancellationTokenForThisRead)
        {
            // Note that even though Stream.ReadAsync has an overload that takes a CancellationToken, that
            // does not actually work for network sockets (https://stackoverflow.com/questions/12421989/networkstream-readasync-with-a-cancellation-token-never-cancels).
            // So we must use AsyncHelpers.AllowCancellation to wrap it in a cancellable task.
            return DoWithTimeout(_readTimeout, cancellationTokenForThisRead,
                token => AllowCancellation(
                    _stream.ReadAsync(b, offset, size),
                    token));
        }

        internal class IncrementalMessageDataInputStream : Stream
        {
            private readonly EventParser _parser;
            private bool _haveChunk = true;
            private int _readOffset = 0;
            private bool _closed = false;

            public IncrementalMessageDataInputStream(EventParser parser)
            {
                _parser = parser;
            }

            public override void Close()
            {
                if (_closed)
                {
                    return;
                }
                if (_parser._currentMessageDataStream == this)
                {
                    _parser._currentMessageDataStream = null;
                    _parser._skipRestOfMessage = true;
                }
                _closed = true;
            }

            public override bool CanRead => true;

            public override bool CanSeek => throw new NotImplementedException();

            public override bool CanWrite => false;

            public override long Length => throw new NotImplementedException();

            public override long Position
            {
                get => throw new NotImplementedException();
                set => throw new NotImplementedException();
            }

            public override void Flush() => throw new NotImplementedException();

            public override long Seek(long offset, SeekOrigin origin) =>
                throw new NotImplementedException();

            public override void SetLength(long value) => throw new NotImplementedException();

            public override void Write(byte[] buffer, int offset, int count) =>
                throw new NotImplementedException();

            public override int Read(byte[] buffer, int offset, int count) =>
                AsyncHelpers.WaitSafely(() => ReadAsync(buffer, offset, count, CancellationToken.None));

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
                CancellationToken cancellationToken)
            {
                while (true) // we will loop until we have either some data or EOF
                {
                    if (count <= 0 || _closed)
                    {
                        return 0;
                    }

                    // Possible states:
                    // (A) We are consuming (or skipping) a chunk that was already loaded by lineParser.
                    if (_haveChunk)
                    {
                        if (_parser._skipRestOfLine)
                        {
                            _parser._skipRestOfLine = !_parser._lineEnded;
                            _haveChunk = false;
                            continue; // We'll go to (B) in the next loop
                        }

                        int availableSize = _parser._chunk.Length - _readOffset;
                        if (availableSize == 0)
                        {
                            // We don't want to return "zero bytes read" because in .NET that means EOF
                            _haveChunk = false;
                            continue; // We'll go to (B) in the next loop
                        }
                        if (availableSize > count)
                        {
                            Buffer.BlockCopy(_parser._chunk.Data, _parser._chunk.Offset + _readOffset,
                                buffer, offset, count);
                            _readOffset += count;
                            return count;
                        }
                        Buffer.BlockCopy(_parser._chunk.Data, _parser._chunk.Offset + _readOffset,
                            buffer, offset, availableSize);
                        _haveChunk = false; // We'll go to (B) on the next call
                        _readOffset = 0;
                        return availableSize;
                    }

                    // (B) We must ask lineParser to give us another chunk of a not-yet-finished line.
                    if (!_parser._lineEnded)
                    {
                        if (!await CanGetNextChunk(cancellationToken))
                        {
                            // The underlying SSE stream has run out of data while we were still trying to
                            // read the rest of the message. This is an abnormal condition, so we'll treat
                            // it as an exception, rather than just returning -1 to indicate EOF.
                            throw new StreamClosedWithIncompleteMessageException();
                        }
                        _haveChunk = true;
                        continue; // We'll go to (A) in the next loop
                    }

                    // (C) The previous line was done; ask lineParser to give us the next line (or at
                    // least the first chunk of it).
                    if (!await CanGetNextChunk(cancellationToken))
                    {
                        // See comment above about abnormal termination. Even if we just finished
                        // reading a complete line of data, the message is incomplete because we didn't
                        // see a blank line.
                        throw new StreamClosedWithIncompleteMessageException();
                    }
                    if (_parser._lineEnded && _parser._chunk.Length == 0)
                    {
                        // Blank line means end of message - close this stream and return EOF. This is a
                        // normal condition: the stream of data for this message is done because the
                        // message is finished.
                        _closed = true;
                        _parser.ResetState();
                        return 0;
                    }
                    // If it's not a blank line then it should have a field name.
                    var fieldName = _parser.ParseFieldName();
                    if (fieldName != Constants.DataField)
                    {
                        // If it's any field other than "data:", there's no way for us to do anything
                        // with it at this point-- that's an inherent limitation of streaming data mode.
                        // So we'll just skip the line.
                        _parser._skipRestOfLine = !_parser._lineEnded;
                        continue; // we'll go to (A) in the next loop
                    }
                    // We are starting another "data:" line. Since we have already read at least one
                    // data line before we get to this point, we should return a linefeed at this point.
                    buffer[0] = (byte)'\n';
                    _haveChunk = true; // We'll go to (A) on the next call
                    return 1;
                }
            }

            private async Task<bool> CanGetNextChunk(CancellationToken cancellationToken)
            {
                CancellationToken chainedToken =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken,
                        _parser._cancellationToken
                    ).Token;
                try
                {
                    await _parser.GetNextChunk(chainedToken);
                }
                catch (StreamClosedByServerException)
                {
                    Close();
                    return false;
                }
                return true;
            }
        }
    }
}
