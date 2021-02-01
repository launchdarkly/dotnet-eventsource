using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.Routing;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.EventSource.Tests
{
    // For most of the EventSource tests, it's adequate to use a fake HttpMessageHandler that provides
    // the desired response data without really doing HTTP. But we do want to be sure that our I/O logic
    // works as intended with a real HTTP client, so this class does some basic EventSource tests
    // against an embedded HTTP server.

    public class EventSourceEndToEndTest : BaseTest
    {
        public EventSourceEndToEndTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public void ReceiveEventStreamInChunks()
        {
            // This simply verifies that chunked streaming works as expected and that events are being
            // parsed correctly regardless of how the chunks line up with the events.

            var eventData = new List<string>();
            var chunks = new List<string>();
            for (var i = 0; i < 200; i++)
            {
                eventData.Add(string.Format("data{0}", i) + new string('x', i % 7));
            }
            var allBody = string.Concat(eventData.Select(data => "data:" + data + "\n\n"));
            for (var pos = 0; ;)
            {
                int i = chunks.Count;
                int chunkSize = i % 20 + 1;
                if (pos + chunkSize >= allBody.Length)
                {
                    chunks.Add(allBody.Substring(pos));
                    break;
                }
                chunks.Add(allBody.Substring(pos, chunkSize));
                pos += chunkSize;
            }

            using (var server = StartWebServerOnAvailablePort(out var uri, ctx =>
            {
                ctx.Response.ContentType = "text/event-stream";
                ctx.Response.SendChunked = true;
                var stream = ctx.Response.OutputStream;
                foreach (var chunk in chunks)
                {
                    WriteChunk(stream, chunk);
                }
            }))
            {
                var expectedActions = new List<EventSink.Action>();
                expectedActions.Add(EventSink.OpenedAction());
                foreach (var data in eventData)
                {
                    expectedActions.Add(EventSink.MessageReceivedAction(new MessageEvent(MessageEvent.DefaultName, data, uri)));
                }

                var config = Configuration.Builder(uri).LogAdapter(_testLogging).Build();
                using (var es = new EventSource(config))
                {
                    var sink = new EventSink(es);
                    _ = es.StartAsync();
                    sink.ExpectActions(expectedActions.ToArray());
                }
            }
        }

        [Fact]
        public void ReadTimeoutIsDetectedInDefaultStringStreamReadingMode()
        {
            TimeSpan readTimeout = TimeSpan.FromMilliseconds(200);
            using (var server = StartWebServerOnAvailablePort(out var uri, ctx =>
            {
                ctx.Response.ContentType = "text/event-stream";
                ctx.Response.SendChunked = true;
                var stream = ctx.Response.OutputStream;
                WriteChunk(stream, "data: event1\n\ndata: e");
                Thread.Sleep(readTimeout + readTimeout);
                WriteChunk(stream, "vent2\n\n");
            }))
            {
                var config = Configuration.Builder(uri).LogAdapter(_testLogging)
                    .ReadTimeout(readTimeout).Build();
                using (var es = new EventSource(config))
                {
                    var sink = new EventSink(es) { Output = _testLogger.Debug };
                    _ = es.StartAsync();
                    sink.ExpectActions(
                        EventSink.OpenedAction(),
                        EventSink.MessageReceivedAction(new MessageEvent(MessageEvent.DefaultName, "event1", uri)),
                        EventSink.ErrorAction(new ReadTimeoutException()),
                        EventSink.ClosedAction()
                        );
                }
            }
        }

        [Fact]
        public void ReadTimeoutIsDetectedInRawUtf8ReadingMode()
        {
            TimeSpan readTimeout = TimeSpan.FromMilliseconds(200);
            using (var server = StartWebServerOnAvailablePort(out var uri, ctx =>
            {
                ctx.Response.ContentType = "text/event-stream";
                ctx.Response.SendChunked = true;
                var stream = ctx.Response.OutputStream;
                WriteChunk(stream, "data: event1\n\ndata: e");
                Thread.Sleep(readTimeout + readTimeout);
                WriteChunk(stream, "vent2\n\n");
            }))
            {
                var config = Configuration.Builder(uri).LogAdapter(_testLogging)
                    .ReadTimeout(readTimeout)
                    .DefaultEncoding(Encoding.UTF8).PreferDataAsUtf8Bytes(true).Build();
                using (var es = new EventSource(config))
                {
                    var sink = new EventSink(es) { Output = _testLogger.Debug };
                    _ = es.StartAsync();
                    sink.ExpectActions(
                        EventSink.OpenedAction(),
                        EventSink.MessageReceivedAction(new MessageEvent(MessageEvent.DefaultName, "event1", uri)),
                        EventSink.ErrorAction(new ReadTimeoutException()),
                        EventSink.ClosedAction()
                        );
                }
            }
        }

        private static void WriteChunk(Stream stream, string data)
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }

        private static WebServer StartWebServerOnAvailablePort(out Uri serverUri, Action<IHttpContext> handler)
        {
            var module = new SimpleModule(handler);

            for (int port = 10000; ; port++)
            {
                var server = new WebServer(port).WithModule(module);
                try
                {
                    _ = server.RunAsync();
                }
                catch (HttpListenerException)
                {
                    continue;
                }
                serverUri = new Uri(string.Format("http://localhost:{0}", port));
                return server;
            }
        }

        // A simple web server handler for use with EmbedIO, delegating to a function you provide.
        private sealed class SimpleModule : IWebModule
        {
            private readonly Action<IHttpContext> _action;

            public SimpleModule(Action<IHttpContext> action)
            {
                _action = action;
            }

            public string BaseRoute => throw new NotImplementedException();

            public bool IsFinalHandler => throw new NotImplementedException();

            public ExceptionHandlerCallback OnUnhandledException { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public HttpExceptionHandlerCallback OnHttpException { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public Task HandleRequestAsync(IHttpContext context)
            {
                _action(context);
                return Task.CompletedTask;
            }

            public RouteMatch MatchUrlPath(string urlPath) =>
                RouteMatch.UnsafeFromBasePath("/", urlPath);

            public void Start(CancellationToken cancellationToken) { }
        }
    }
}
