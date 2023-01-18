using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.EventSource.Events;
using LaunchDarkly.Logging;

using static LaunchDarkly.EventSource.Internal.AsyncHelpers;

namespace LaunchDarkly.EventSource.Internal
{
    /// <summary>
    /// An internal class containing helper methods to parse Server Sent Event data.
    /// </summary>
    internal sealed class EventParser
    {
        private const int ReadBufferSize = 1000;
        private const int ValueBufferInitialCapacity = 1000;

        private readonly Stream _stream;
        private readonly BufferedLineParser _lineParser;
        private readonly TimeSpan _readTimeout;
        private readonly Uri _origin;
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

        public EventParser(
            Stream stream,
            TimeSpan readTimeout,
            Uri origin,
            CancellationToken cancellationToken,
            Logger logger
            )
        {
            _stream = stream;
            _lineParser = new BufferedLineParser(
                ReadFromStream,
                ReadBufferSize
                );
            _readTimeout = readTimeout;
            _origin = origin;
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
            await GetNextChunk(); // throws exception if stream has closed

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
                var dataSpan = new Utf8ByteSpan(_dataBuffer.GetBuffer(), 0, (int)_dataBuffer.Length);
                MessageEvent message = new MessageEvent(name, dataSpan, _lastEventId, _origin);
                // We've now taken ownership of the original buffer; ResetState will null out the
                // previous reference to it so a new one will be created next time
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
                // Accumulate this data in a buffer until we've seen the end of the event.
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

            // For any field other than "data:", there can only be a single line of a value and
            // we just get the whole value as a string. If the whole line fits into the buffer
            // then we can do this in one step; otherwise we'll accumulate chunks in another
            // buffer until the line is done.
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

        private async Task GetNextChunk()
        {
            var chunk = await _lineParser.ReadAsync(); // throws exception if stream has closed
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

        private Task<int> ReadFromStream(byte[] b, int offset, int size)
        {
            // Note that even though Stream.ReadAsync has an overload that takes a CancellationToken, that
            // does not actually work for network sockets (https://stackoverflow.com/questions/12421989/networkstream-readasync-with-a-cancellation-token-never-cancels).
            // So we must use AsyncHelpers.AllowCancellation to wrap it in a cancellable task.
            return DoWithTimeout(_readTimeout, _cancellationToken,
                token => AllowCancellation(
                    _stream.ReadAsync(b, offset, size),
                    token));
        }
    }
}
