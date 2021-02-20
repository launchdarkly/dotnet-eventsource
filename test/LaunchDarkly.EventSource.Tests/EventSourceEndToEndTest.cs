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
            for (var i = 0; i < 200; i++)
            {
                eventData.Add(string.Format("data{0}", i) + new string('x', i % 7));
            }
            var allBody = string.Concat(eventData.Select(data => "data:" + data + "\n\n"));
            var allEventsReceived = new EventWaitHandle(false, EventResetMode.ManualReset);

            IEnumerable<string> DoChunks()
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
                allEventsReceived.WaitOne();
            }

            try
            {
                using (var server = StartWebServerOnAvailablePort(out var uri,
                    RespondWithChunks("text/event-stream", DoChunks)))
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
            finally
            {
                allEventsReceived.Set();
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReadTimeoutIsDetected(bool utf8Mode)
        {
            TimeSpan readTimeout = TimeSpan.FromMilliseconds(2000);
            IEnumerable<string> DoChunks()
            {
                yield return "";
                yield return "data: event1\n\ndata: e";
                Thread.Sleep(readTimeout + readTimeout);
                yield return "vent2\n\n";
            }
            using (var server = StartWebServerOnAvailablePort(out var uri, RespondWithChunks("text/event-stream", DoChunks)))
            {
                var config = Configuration.Builder(uri)
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
                        EventSink.MessageReceivedAction(new MessageEvent(MessageEvent.DefaultName, "event1", uri)),
                        EventSink.ErrorAction(new ReadTimeoutException()),
                        EventSink.ClosedAction()
                        );
                }
            }
        }

        private static WebServer StartWebServerOnAvailablePort(out Uri serverUri, Action<IHttpContext> handler)
        {
            var module = new SimpleModule(handler);

            for (int port = 10000; ; port++)
            {
                var options = new WebServerOptions()
                    .WithUrlPrefix($"http://*:{port}")
                    .WithMode(HttpListenerMode.EmbedIO);
                var server = new WebServer(options).WithModule(module);
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

        private static Action<IHttpContext> RespondWithChunks(string contentType, Func<IEnumerable<string>> chunks) =>
            ctx =>
            {
                ctx.Response.ContentType = contentType;
                ctx.Response.SendChunked = true;
                var stream = ctx.Response.OutputStream;
                foreach (var chunk in chunks())
                {
                    var bytes = Encoding.UTF8.GetBytes(chunk);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush();
                }
            };

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
