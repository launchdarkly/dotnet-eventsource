using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using LaunchDarkly.TestHelpers.HttpTest;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.EventSource.Tests
{
    public class EventSourceHttpBehaviorTest : BaseTest
    {
        public EventSourceHttpBehaviorTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public async Task CustomHttpClientIsNotClosedWhenEventSourceCloses()
        {
            using (var server = HttpServer.Start(Handlers.Status(200)))
            {
                using (var client = new HttpClient())
                {
                    var es = new EventSource(Configuration.Builder(server.Uri).HttpClient(client).Build());
                    es.Close();

                    await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, server.Uri));
                }
            }
        }

        [Fact]
        public void DefaultMethod()
        {
            using (var server = HttpServer.Start(EmptyStreamThatStaysOpen))
            {
                using (var es = MakeEventSource(server.Uri))
                {
                    _ = Task.Run(es.StartAsync);

                    var req = server.Recorder.RequireRequest();
                    Assert.Equal("GET", req.Method);
                }
            }
        }

        [Fact]
        public void CustomMethod()
        {
            using (var server = HttpServer.Start(EmptyStreamThatStaysOpen))
            {
                using (var es = MakeEventSource(server.Uri, builder => builder.Method(HttpMethod.Post)))
                {
                    _ = Task.Run(es.StartAsync);

                    var req = server.Recorder.RequireRequest();
                    Assert.Equal("POST", req.Method);
                }
            }
        }

        [Fact]
        public void CustomMethodWithRequestBody()
        {
            using (var server = HttpServer.Start(EmptyStreamThatStaysOpen))
            {
                var content = "{}";
                Func<HttpContent> contentFn = () => new StringContent(content);

                using (var es = MakeEventSource(server.Uri, builder =>
                    builder.Method(HttpMethod.Post).RequestBodyFactory(contentFn)))
                {
                    _ = Task.Run(es.StartAsync);

                    var req = server.Recorder.RequireRequest();
                    Assert.Equal(content, req.Body);
                }
            }
        }

        [Fact]
        public void AcceptHeaderIsAlwaysPresent()
        {
            using (var server = HttpServer.Start(EmptyStreamThatStaysOpen))
            {
                using (var es = MakeEventSource(server.Uri))
                {
                    _ = Task.Run(es.StartAsync);

                    var req = server.Recorder.RequireRequest();
                    Assert.Contains(Constants.EventStreamContentType, req.Headers.GetValues(Constants.AcceptHttpHeader));
                }
            }
        }

        [Fact]
        public void LastEventIdHeaderIsNotSetByDefault()
        {
            using (var server = HttpServer.Start(EmptyStreamThatStaysOpen))
            {
                using (var es = MakeEventSource(server.Uri))
                {
                    _ = Task.Run(es.StartAsync);

                    var req = server.Recorder.RequireRequest();
                    Assert.Null(req.Headers.Get(Constants.LastEventIdHttpHeader));
                }
            }
        }

        [Fact]
        public void LastEventIdHeaderIsSetIfConfigured()
        {
            using (var server = HttpServer.Start(EmptyStreamThatStaysOpen))
            {
                var lastEventId = "abc123";

                using (var es = MakeEventSource(server.Uri, builder => builder.LastEventId(lastEventId)))
                {
                    _ = Task.Run(es.StartAsync);

                    var req = server.Recorder.RequireRequest();
                    Assert.Equal(lastEventId, req.Headers.Get(Constants.LastEventIdHttpHeader));
                }
            }
        }

        [Fact]
        public void CustomRequestHeaders()
        {
            using (var server = HttpServer.Start(EmptyStreamThatStaysOpen))
            {
                var headers = new Dictionary<string, string> { { "User-Agent", "mozilla" }, { "Authorization", "testing" } };

                using (var es = MakeEventSource(server.Uri, builder => builder.RequestHeaders(headers)))
                {
                    _ = Task.Run(es.StartAsync);

                    var req = server.Recorder.RequireRequest();
                    Assert.True(headers.All(
                        item => req.Headers.Get(item.Key) == item.Value
                    ));
                }
            }
        }

        [Fact]
        public void HttpRequestModifier()
        {
            using (var server = HttpServer.Start(EmptyStreamThatStaysOpen))
            {
                var headers = new Dictionary<string, string> { { "User-Agent", "mozilla" }, { "Authorization", "testing" } };

                Action<HttpRequestMessage> modifier = request =>
                {
                    request.RequestUri = new Uri(request.RequestUri.ToString() + "-modified");
                };
                using (var es = MakeEventSource(server.Uri, builder => builder.HttpRequestModifier(modifier)))
                {
                    _ = Task.Run(es.StartAsync);

                    var req = server.Recorder.RequireRequest();
                    Assert.EndsWith("-modified", req.Path);
                }
            }
        }

        [Fact]
        public void ReceiveEventStreamInChunks()
        {
            // This simply verifies that chunked streaming works as expected and that events are being
            // parsed correctly regardless of how the chunks line up with the events.

            var eventData = new List<string>();
            for (var i = 0; i < 200; i++)
            {
                eventData.Add(string.Format("data{0}", i) + new string('x', i % 7));
            }
            var allBody = string.Concat(eventData.Select(data => "data:" + data + "\n\n"));
            var allEventsReceived = new TaskCompletionSource<bool>();

            IEnumerable<string> MakeChunks()
            {
                var i = 0;
                for (var pos = 0; ;)
                {
                    int chunkSize = i % 20 + 1;
                    if (pos + chunkSize >= allBody.Length)
                    {
                        yield return allBody.Substring(pos);
                        break;
                    }
                    yield return allBody.Substring(pos, chunkSize);
                    pos += chunkSize;
                    i++;
                }
            }

            try
            {
                Handler streamHandler = Handlers.StartChunks("text/event-stream")
                    .Then(async ctx =>
                    {
                        foreach (var s in MakeChunks())
                        {
                            await Handlers.WriteChunkString(s)(ctx);
                        }
                        await allEventsReceived.Task;
                    });
                using (var server = HttpServer.Start(streamHandler))
                {
                    var expectedActions = new List<EventSink.Action>();
                    expectedActions.Add(EventSink.OpenedAction());
                    foreach (var data in eventData)
                    {
                        expectedActions.Add(EventSink.MessageReceivedAction(new MessageEvent(MessageEvent.DefaultName, data, server.Uri)));
                    }

                    var config = Configuration.Builder(server.Uri).LogAdapter(_testLogging).Build();
                    using (var es = new EventSource(config))
                    {
                        var sink = new EventSink(es);
                        _ = es.StartAsync();
                        sink.ExpectActions(expectedActions.ToArray());
                    }
                }
            }
            finally
            {
                allEventsReceived.SetResult(true);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReadTimeoutIsDetected(bool utf8Mode)
        {
            TimeSpan readTimeout = TimeSpan.FromMilliseconds(200);
            var streamHandler = Handlers.StartChunks("text/event-stream")
                .Then(Handlers.WriteChunkString("data: event1\n\ndata: e"))
                .Then(Handlers.Delay(readTimeout + readTimeout))
                .Then(Handlers.WriteChunkString("vent2\n\n"));
            using (var server = HttpServer.Start(streamHandler))
            {
                var config = Configuration.Builder(server.Uri)
                    .LogAdapter(_testLogging)
                    .ReadTimeout(readTimeout)
                    .PreferDataAsUtf8Bytes(utf8Mode)
                    .Build();
                using (var es = new EventSource(config))
                {
                    var sink = new EventSink(es) { Output = _testLogger.Debug };
                    _ = es.StartAsync();
                    sink.ExpectActions(
                        EventSink.OpenedAction(),
                        EventSink.MessageReceivedAction(new MessageEvent(MessageEvent.DefaultName, "event1", server.Uri)),
                        EventSink.ErrorAction(new ReadTimeoutException()),
                        EventSink.ClosedAction()
                        );
                }
            }
        }
        [Fact]
        public void ErrorForIncorrectContentType()
        {
            var response = Handlers.Status(200).Then(Handlers.BodyString("text/html", "testing"));
            using (var server = HttpServer.Start(response))
            {
                using (var es = MakeEventSource(server.Uri))
                {
                    var eventSink = new EventSink(es, _testLogging);
                    _ = Task.Run(es.StartAsync);

                    var errorAction = eventSink.ExpectAction();
                    var ex = Assert.IsType<EventSourceServiceCancelledException>(errorAction.Exception);
                    Assert.Matches(".*content type.*text/html", ex.Message);
                }
            }
        }

        [Theory]
        [InlineData(HttpStatusCode.NoContent)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.RequestTimeout)]
        [InlineData(HttpStatusCode.Unauthorized)]
        public void ErrorForInvalidHttpStatus(HttpStatusCode statusCode)
        {
            using (var server = HttpServer.Start(Handlers.Status(statusCode)))
            {
                using (var es = MakeEventSource(server.Uri))
                {
                    var eventSink = new EventSink(es, _testLogging);
                    _ = Task.Run(es.StartAsync);

                    var errorAction = eventSink.ExpectAction();
                    var ex = Assert.IsType<EventSourceServiceUnsuccessfulResponseException>(errorAction.Exception);
                    Assert.Equal((int)statusCode, ex.StatusCode);
                }
            }
        }
    }
}
