using System;
using System.Collections.Generic;
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
    public class EventParserStreamingDataTest : BaseTest
    {
        private readonly Uri Origin = new Uri("http://origin");
        private const int DefaultBufferSize = 200;

        private EventParser _parser;

        public EventParserStreamingDataTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public async Task SingleLineDataInSingleChunk()
        {
            InitParser(20, new FakeInputStream("data: line1\n\n"));

            var m = await RequireMessage();
            Assert.True(m.IsStreamingData);
            Assert.Equal("line1", await ReadAllAsync(m.DataStream));
            await AssertEof();
        }

        [Fact]
        public async Task CanReadSynchronously()
        {
            InitParser(20, new FakeInputStream("data: abcdefghijklmnopqrstuvwxyz\n\n"));

            var m = await RequireMessage();
            Assert.True(m.IsStreamingData);
            Assert.Equal("abcdefghijklmnopqrstuvwxyz", ReadAllSync(m.DataStream));
            await AssertEof();
        }

        [Fact]
        public async Task SingleLineDataInMultipleChunks()
        {
            InitParser(20, new FakeInputStream("data: line1\n\n"));

            var m = await RequireMessage();
            Assert.True(m.IsStreamingData);
            Assert.Equal("line1", await ReadAllAsync(m.DataStream));
            await AssertEof();
        }

        [Fact]
        public async Task MultiLineDataInSingleChunk()
        {
            InitParser(100, new FakeInputStream("data: line1\ndata: line2\n\n"));

            var m = await RequireMessage();
            Assert.True(m.IsStreamingData);
            Assert.Equal("line1\nline2", await ReadAllAsync(m.DataStream));
            await AssertEof();
        }

        [Fact]
        public async Task MultiLineDataInMultipleChunks()
        {
            InitParser(20, new FakeInputStream("data: abcdefghijklmnopqrstuvwxyz\ndata: 1234567890\n\n"));

            var m = await RequireMessage();
            Assert.True(m.IsStreamingData);
            Assert.Equal("abcdefghijklmnopqrstuvwxyz\n1234567890", await ReadAllAsync(m.DataStream));
            await AssertEof();
        }

        [Fact]
        public async Task EventNameAndIdArePreservedIfTheyAreBeforeData()
        {
            InitParser(20, new FakeInputStream("event: hello\nid: id1\ndata: line1\n\n"));

            var m = await RequireMessage();
            Assert.True(m.IsStreamingData);
            Assert.Equal("hello", m.Name);
            Assert.Equal("id1", m.LastEventId);
            Assert.Equal("line1", await ReadAllAsync(m.DataStream));
            await AssertEof();
        }

        [Fact]
        public async Task EventNameAndIdAreIgnoredIfTheyAreAfterData()
        {
            InitParser(20, new FakeInputStream("data: line1\nevent: hello\nid: id1\n\n"));

            var m = await RequireMessage();
            Assert.True(m.IsStreamingData);
            Assert.Equal(MessageEvent.DefaultName, m.Name);
            Assert.Null(m.LastEventId);
            Assert.Equal("line1", await ReadAllAsync(m.DataStream));
            await AssertEof();
        }

        [Fact]
        public async Task CanRequireEventName()
        {
            InitParser(20, new FakeInputStream(
                    "data: line1\nevent: hello\nid: id1\n\n",
                    "event: world\ndata: line2\nid: id2\n\n"
                ),
                "event"
                );

            var m1 = await RequireMessage();
            Assert.False(m1.IsStreamingData);
            Assert.Equal("hello", m1.Name);
            Assert.Equal("id1", m1.LastEventId);
            Assert.Equal("line1", await ReadAllAsync(m1.DataStream));

            var m2 = await RequireMessage();
            Assert.True(m2.IsStreamingData);
            Assert.Equal("world", m2.Name);
            Assert.Equal("id1", m2.LastEventId); // "id: id2" was ignored because it came after "data:"
            Assert.Equal("line2", await ReadAllAsync(m2.DataStream));

            await AssertEof();
        }

        [Fact]
        public async Task ChunkSizeIsGreaterThanReadBufferSize()
        {
            var s = MakeStringOfLength(11000);
            var streamData = "data: " + s + "\n\n";
            InitParser(100, new FakeInputStream(streamData));

            var m = await RequireMessage();
            Assert.True(m.IsStreamingData);
            Assert.Equal(s, await ReadAllAsync(m.DataStream));
        }

        [Fact]
        public async Task InvalidLineWithinEvent()
        {
            InitParser(20, new FakeInputStream(
                "data: data1\n",
                "ignorethis: meaninglessline\n",
                "data: data2\n\n"
                ));

            var m = await RequireMessage();
            Assert.True(m.IsStreamingData);
            Assert.Equal("data1\ndata2", await ReadAllAsync(m.DataStream));
            await AssertEof();
        }

        [Fact]
        public async Task IncompletelyReadEventIsSkippedIfAnotherMessageIsRead()
        {
            InitParser(20, new FakeInputStream(
                "data: hello1\n",
                "data: hello2\n",
                "event: hello\n",
                "id: id1\n\n" +
                "data: world\n\n"
                ));

            var m1 = await RequireMessage();
            Assert.True(m1.IsStreamingData);
            Assert.Equal("he", await ReadUpToLimitAsync(m1.DataStream, 2));

            var m2 = await RequireMessage();
            Assert.True(m2.IsStreamingData);
            Assert.Equal("world", await ReadAllAsync(m2.DataStream));

            await AssertEof();
        }

        [Fact]
        public async Task UnderlyingStreamIsClosedImmediatelyAfterEndOfEvent()
        {
            InitParser(100, new FakeInputStream("data: hello\n\n"));

            var m = await RequireMessage();
            Assert.True(m.IsStreamingData);
            Assert.Equal("hello", await ReadAllAsync(m.DataStream));

            await AssertStreamEof(m.DataStream);
        }

        [Fact]
        public async Task UnderlyingStreamIsClosedBeforeEndOfEventAtEndOfLine()
        {
            InitParser(100, new FakeInputStream("data: hello\n"));

            var m = await RequireMessage();
            Assert.True(m.IsStreamingData);
            Assert.Equal("hello", await ReadUpToLimitAsync(m.DataStream, 5));

            await AssertIncompleteMessageError(m.DataStream);
        }

        private void InitParser(int bufferSize, FakeInputStream stream,
            params string[] expectFields)
        {
            stream.Logger = _testLogger;
            _parser = new EventParser(
                stream,
                bufferSize,
                TimeSpan.FromDays(1),
                Origin,
                true,
                expectFields.Length == 0 ? null : new HashSet<string>(expectFields),
                CancellationToken.None,
                _testLogger);
        }

        private Task<IEvent> RequireEvent() => _parser.NextEventAsync();

        private async Task<MessageEvent> RequireMessage() =>
            Assert.IsType<MessageEvent>(await _parser.NextEventAsync());

        private async Task AssertEof() =>
            await Assert.ThrowsAnyAsync<StreamClosedByServerException>(
                () => _parser.NextEventAsync());

        private async Task<string> ReadAllAsync(Stream stream) =>
            await new StreamReader(stream, Encoding.UTF8).ReadToEndAsync();

        private string ReadAllSync(Stream stream) =>
            new StreamReader(stream, Encoding.UTF8).ReadToEnd();

        private async Task<string> ReadUpToLimitAsync(Stream stream, int limit)
        {
            var chunk = new byte[100];
            var buffer = new MemoryStream();
            while (buffer.Length < limit)
            {
                int n = await stream.ReadAsync(chunk, 0, limit - (int)buffer.Length);
                if (n <= 0)
                {
                    break;
                }
                buffer.Write(chunk, 0, n);
            }
            return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        }

        private async Task AssertStreamEof(Stream stream) =>
            Assert.Equal(0, await stream.ReadAsync(new byte[1], 0, 1));

        private async Task AssertIncompleteMessageError(Stream stream) =>
            await Assert.ThrowsAnyAsync<StreamClosedWithIncompleteMessageException>(
                () => stream.ReadAsync(new byte[1], 0, 1));
    }
}
