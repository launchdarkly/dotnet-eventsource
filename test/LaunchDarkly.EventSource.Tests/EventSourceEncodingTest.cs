using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.EventSource.Tests
{
    public class EventSourceEncodingTest : BaseTest
    {
        public EventSourceEncodingTest(ITestOutputHelper testOutput) : base(testOutput) { }

        struct StreamTestParams
        {
            public StreamAction[] StreamActions { get; set; }
            public EventSink.Action[] ExpectedEventActions { get; set; }
        }

        private static readonly StreamTestParams BasicStreamTestParams = new StreamTestParams
        {
            StreamActions = new StreamAction[]
            {
                StreamAction.Write(": hello\n"),
                StreamAction.Write("data: value1"),
                StreamAction.Write("\n\nevent: event2\ndata:"),
                StreamAction.Write("ça\ndata: qu"),
                StreamAction.Write("é\n"),
                StreamAction.Write("\n"),
                StreamAction.Write("data:" + MakeLongString(0, 500)),
                StreamAction.Write(MakeLongString(500, 1000) + "\n\n")
            },

            ExpectedEventActions = new EventSink.Action[]
            {
                EventSink.OpenedAction(ReadyState.Open),
                EventSink.CommentReceivedAction(": hello"),
                EventSink.MessageReceivedAction(new MessageEvent("message", "value1", null, _uri)),
                EventSink.MessageReceivedAction(new MessageEvent("event2", "ça\nqué", null, _uri)),
                EventSink.MessageReceivedAction(new MessageEvent("message",
                    MakeLongString(0, 500) + MakeLongString(500, 1000), null, _uri))
            }
        };

        private static string MakeLongString(int startNum, int endNum)
        {
            // This is meant to verify that we're able to read event data from the stream
            // that doesn't all fit into our initial read buffer at once.
            var ret = new StringBuilder();
            for (var i = startNum; i < endNum; i++)
            {
                ret.Append(i).Append("!");
            }
            return ret.ToString();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CanReceiveUtf8EventDataAsStrings(bool setExplicitEncoding)
        {
            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream(
                setExplicitEncoding ? Encoding.UTF8 : null,
                BasicStreamTestParams.StreamActions));

            var config = Configuration.Builder(_uri).HttpMessageHandler(handler)
                .LogAdapter(_testLogging)
                .Build();
            using (var es = new EventSource(config))
            {
                var eventSink = new EventSink(es, _testLogging) { ExpectUtf8Data = false };

                _ = Task.Run(es.StartAsync);

                eventSink.ExpectActions(BasicStreamTestParams.ExpectedEventActions);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CanReceiveUtf8EventDataAsBytes(bool setExplicitEncoding)
        {
            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream(
                setExplicitEncoding ? Encoding.UTF8 : null,
                BasicStreamTestParams.StreamActions));

            var config = Configuration.Builder(_uri).HttpMessageHandler(handler)
                .LogAdapter(_testLogging)
                .PreferDataAsUtf8Bytes(true)
                .Build();
            using (var es = new EventSource(config))
            {
                var eventSink = new EventSink(es, _testLogging) { ExpectUtf8Data = true };

                _ = Task.Run(es.StartAsync);

                eventSink.ExpectActions(BasicStreamTestParams.ExpectedEventActions);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void NonUtf8EncodingIsReadAsStrings(bool preferUtf8Data)
        {
            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream(
                Encoding.GetEncoding("iso-8859-1"),
                BasicStreamTestParams.StreamActions));

            var config = Configuration.Builder(_uri).HttpMessageHandler(handler)
                .LogAdapter(_testLogging)
                .PreferDataAsUtf8Bytes(preferUtf8Data)
                .Build();
            using (var es = new EventSource(config))
            {
                var sink = new EventSink(es, _testLogging) { ExpectUtf8Data = false };

                _ = Task.Run(es.StartAsync);

                sink.ExpectActions(BasicStreamTestParams.ExpectedEventActions);
            }
        }
    }
}
