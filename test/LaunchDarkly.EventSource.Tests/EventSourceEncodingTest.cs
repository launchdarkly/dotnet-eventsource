using System.Text;
using System.Threading.Tasks;
using LaunchDarkly.TestHelpers;
using LaunchDarkly.TestHelpers.HttpTest;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.EventSource.Tests
{
    public class EventSourceEncodingTest : BaseTest
    {
        public EventSourceEncodingTest(ITestOutputHelper testOutput) : base(testOutput) { }

        private static readonly string[] streamChunks = new string[]
        {
            ": hello\n",
            "data: value1",
            "\n\nevent: event2\ndata:",
            "ça\ndata: qu",
            "é\n",
            "\n",
            "data:" + MakeLongString(0, 500),
            MakeLongString(500, 1000) + "\n\n"
        };

        private static readonly EventSink.Action[] expectedEventActions = new EventSink.Action[]
        {
            EventSink.OpenedAction(ReadyState.Open),
            EventSink.CommentReceivedAction(": hello"),
            EventSink.MessageReceivedAction(new MessageEvent("message", "value1", null, _uri)),
            EventSink.MessageReceivedAction(new MessageEvent("event2", "ça\nqué", null, _uri)),
            EventSink.MessageReceivedAction(new MessageEvent("message",
                MakeLongString(0, 500) + MakeLongString(500, 1000), null, _uri))
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

        private static Handler MakeStreamHandler(Encoding encoding)
        {
            var ret = Handlers.StartChunks("text/event-stream", encoding);
            foreach (var chunk in streamChunks)
            {
                ret = ret.Then(Handlers.WriteChunkString(chunk, encoding));
            }
            return ret.Then(Handlers.Hang());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CanReceiveUtf8EventDataAsStrings(bool setExplicitEncoding)
        {
            using (var server = HttpServer.Start(MakeStreamHandler(setExplicitEncoding ? Encoding.UTF8 : null)))
            {
                var config = Configuration.Builder(server.Uri)
                    .LogAdapter(_testLogging)
                    .Build();
                using (var es = new EventSource(config))
                {
                    var eventSink = new EventSink(es, _testLogging) { ExpectUtf8Data = false };

                    _ = Task.Run(es.StartAsync);

                    eventSink.ExpectActions(expectedEventActions);
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CanReceiveUtf8EventDataAsBytes(bool setExplicitEncoding)
        {
            using (var server = HttpServer.Start(MakeStreamHandler(setExplicitEncoding ? Encoding.UTF8 : null)))
            {
                var config = Configuration.Builder(server.Uri)
                    .LogAdapter(_testLogging)
                    .PreferDataAsUtf8Bytes(true)
                    .Build();
                using (var es = new EventSource(config))
                {
                    var eventSink = new EventSink(es, _testLogging) { ExpectUtf8Data = true };

                    _ = Task.Run(es.StartAsync);

                    eventSink.ExpectActions(expectedEventActions);
                }
            }
        }

        [Fact]
        public void NonUtf8EncodingIsRejected()
        {
            using (var server = HttpServer.Start(MakeStreamHandler(Encoding.GetEncoding("iso-8859-1"))))
            {
                var config = Configuration.Builder(server.Uri)
                    .LogAdapter(_testLogging)
                    .Build();
                using (var es = new EventSource(config))
                {
                    var sink = new EventSink(es, _testLogging) { ExpectUtf8Data = false };
                    _ = Task.Run(es.StartAsync);

                    var errorAction = sink.ExpectAction();
                    var ex = Assert.IsType<EventSourceServiceCancelledException>(errorAction.Exception);
                    Assert.Matches(".*encoding.*8859.*", ex.Message);
                }
            }
        }
    }
}
