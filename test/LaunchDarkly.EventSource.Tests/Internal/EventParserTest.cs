using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.EventSource.Events;
using LaunchDarkly.EventSource.Exceptions;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.EventSource.TestHelpers;

namespace LaunchDarkly.EventSource.Internal
{
    public class EventParserTest : BaseTest
    {
        private readonly Uri Origin = new Uri("http://origin");
        private const int DefaultBufferSize = 200;

        private EventParser _parser;

        public EventParserTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public async Task ReadsSingleLineMessage()
        {
            InitParser(DefaultBufferSize, new FakeInputStream("data: hello\n\n"));

            Assert.Equal(new MessageEvent("message", "hello", null, Origin),
                await RequireEvent());
            await AssertEof();
        }

        [Fact]
        public async Task ReadsSingleLineMessageWithEventName()
        {
            InitParser(DefaultBufferSize, new FakeInputStream(
                "event: hello\n",
                "data: world\n\n"));

            Assert.Equal(new MessageEvent("hello", "world", null, Origin),
                await RequireEvent());
            await AssertEof();
        }

        [Fact]
        public async Task ReadsSingleLineMessageWithid()
        {
            InitParser(DefaultBufferSize, new FakeInputStream("data: hello\n", "id: abc\n\n"));

            Assert.Equal(new MessageEvent("message", "hello", "abc", Origin),
                await RequireEvent());
            await AssertEof();
        }

        [Fact]
        public async Task DoesNotFireMultipleTimesIfSeveralEmptyLines()
        {
            InitParser(DefaultBufferSize, new FakeInputStream("data: hello\n\n"));

            Assert.Equal(new MessageEvent("message", "hello", null, Origin),
                await RequireEvent());
            await AssertEof();
        }

        [Fact]
        public async Task SendsCommentsForLinesStartingWithColon()
        {
            InitParser(DefaultBufferSize, new FakeInputStream(
                ": first comment\n",
                "data: hello\n",
                ":second comment\n\n"
                ));

            Assert.Equal(new CommentEvent("first comment"), await RequireEvent());
            Assert.Equal(new CommentEvent("second comment"), await RequireEvent());
            Assert.Equal(new MessageEvent("message", "hello", null, Origin),
                await RequireEvent());
            // The message is received after the two comments, rather than interleaved between
            // them, because we can't know that the message is actually finished until we see
            // the blank line after the second comment.
            await AssertEof();
        }

        [Fact]
        public async Task ReadsSingleLineMessageWithoutColon()
        {
            InitParser(DefaultBufferSize, new FakeInputStream("data\n\n"));

            Assert.Equal(new MessageEvent("message", "", null, Origin),
                await RequireEvent());
            await AssertEof();
        }

        [Fact]
        public async Task PropertiesAreResetBetweenMessages()
        {
            InitParser(DefaultBufferSize, new FakeInputStream(
                "event: hello\n",
                "data: data1\n",
                "\n",
                "data: data2\n",
                "\n"
                ));

            Assert.Equal(new MessageEvent("hello", "data1", null, Origin),
                await RequireEvent());
            Assert.Equal(new MessageEvent("message", "data2", null, Origin),
                await RequireEvent());
            await AssertEof();
        }

        [Fact]
        public async Task ReadsRetryDirective()
        {
            InitParser(DefaultBufferSize, new FakeInputStream(
                "retry: 7000L\n", // ignored because it's not a numeric string
                "retry: 7000\n"
                ));

            Assert.Equal(new SetRetryDelayEvent(TimeSpan.FromSeconds(7)),
                await RequireEvent());
        }

        [Fact]
        public async Task IgnoresUnknownFieldName()
        {
            InitParser(DefaultBufferSize, new FakeInputStream(
                "data: hello\n",
                "badfield: whatever\n",
                "id: 1\n",
                "\n"
                ));

            Assert.Equal(new MessageEvent("message", "hello", "1", Origin),
                await RequireEvent());
            await AssertEof();
        }

        [Fact]
        public async Task UsesEventIdOfPreviousEventIfNoneSet()
        {
            InitParser(DefaultBufferSize, new FakeInputStream(
                "data: hello\n",
                "id: reused\n",
                "\n",
                "data: world\n",
                "\n"
                ));

            Assert.Equal(new MessageEvent("message", "hello", "reused", Origin),
                await RequireEvent());
            Assert.Equal(new MessageEvent("message", "world", "reused", Origin),
                await RequireEvent());
            await AssertEof();
        }

        [Fact]
        public async Task FieldsCanBeSplitAcrossChunks()
        {
            // This field starts in one chunk and finishes in the next
            var eventName = MakeStringOfLength(DefaultBufferSize + (DefaultBufferSize / 5));

            // This field spans multiple chunks and is also longer than ValueBufferInitialCapacity,
            // so we're verifying that we correctly recreate the buffer afterward
            var id = MakeStringOfLength(EventParser.ValueBufferInitialCapacity + (DefaultBufferSize / 5));

            // Same idea as above, because we know there is a separate buffer for the data field
            var data = MakeStringOfLength(EventParser.ValueBufferInitialCapacity + (DefaultBufferSize / 5));

            // Here we have a field whose name is longer than the buffer, to test our "skip rest of line" logic
            var longInvalidFieldName = MakeStringOfLength(DefaultBufferSize * 2 + (DefaultBufferSize / 5))
                .Replace(':', '_'); // ensure there isn't a colon within the name
            var longInvalidFieldValue = MakeStringOfLength(DefaultBufferSize * 2 + (DefaultBufferSize / 5));

            // This one tests the logic where we are able to parse the field name right away, but the value is long
            var shortInvalidFieldName = "whatever";

            InitParser(DefaultBufferSize, new FakeInputStream(
                "event: " + eventName + "\n",
                "data: " + data + "\n",
                "id: " + id + "\n",
                shortInvalidFieldName + ": " + longInvalidFieldValue + "\n",
                longInvalidFieldName + ": " + longInvalidFieldValue + "\n",
                "\n"
                ));

            Assert.Equal(new MessageEvent(eventName, data, id, Origin),
                await RequireEvent());
            await AssertEof();
        }

        private void InitParser(int bufferSize, FakeInputStream stream)
        {
            stream.Logger = _testLogger;
            _parser = new EventParser(stream, bufferSize, TimeSpan.FromDays(1),
                Origin, false, null, new CancellationTokenSource().Token, _testLogger);
        }

        private Task<IEvent> RequireEvent() => _parser.NextEventAsync();

        private async Task AssertEof() =>
            await Assert.ThrowsAnyAsync<StreamClosedByServerException>(
                () => _parser.NextEventAsync());
    }
}
