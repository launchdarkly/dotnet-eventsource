using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
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
            var handler = new StubMessageHandler(StubResponse.WithStatus(HttpStatusCode.OK));

            using (var client = new HttpClient(handler))
            {
                var es = new EventSource(Configuration.Builder(_uri).HttpClient(client).Build());
                es.Close();

                await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, _uri));
            }
        }

        [Fact]
        public void DefaultMethod()
        {
            var handler = new StubMessageHandler(StubResponse.StartStream());
            using (var es = MakeEventSource(handler))
            {
                _ = Task.Run(es.StartAsync);

                var req = handler.AwaitRequest();
                Assert.Equal(HttpMethod.Get, req.Method);
            }
        }

        [Fact]
        public void CustomMethod()
        {
            var handler = new StubMessageHandler(StubResponse.StartStream());
            using (var es = MakeEventSource(handler, builder => builder.Method(HttpMethod.Post)))
            {
                _ = Task.Run(es.StartAsync);

                var req = handler.AwaitRequest();
                Assert.Equal(HttpMethod.Post, req.Method);
            }
        }

        [Fact]
        public void CustomMethodWithRequestBody()
        {
            var handler = new StubMessageHandler(StubResponse.StartStream());

            HttpContent content = new StringContent("{}");
            Func<HttpContent> contentFn = () => content;

            using (var es = MakeEventSource(handler, builder =>
                builder.Method(HttpMethod.Post).RequestBodyFactory(contentFn)))
            {
                _ = Task.Run(es.StartAsync);

                var req = handler.AwaitRequest();
                Assert.Equal(content, req.Content);
            }
        }

        [Fact]
        public void AcceptHeaderIsAlwaysPresent()
        {
            var handler = new StubMessageHandler(StubResponse.StartStream());

            HttpContent content = new StringContent("{}");
            Func<HttpContent> contentFn = () => content;

            using (var es = MakeEventSource(handler, builder =>
                builder.Method(HttpMethod.Post).RequestBodyFactory(contentFn)))
            {
                _ = Task.Run(es.StartAsync);

                var req = handler.AwaitRequest();
                Assert.True(req.Headers.Contains(Constants.AcceptHttpHeader));
                Assert.Contains(Constants.EventStreamContentType, req.Headers.GetValues(Constants.AcceptHttpHeader));
            }
        }

        [Fact]
        public void LastEventIdHeaderIsNotSetByDefault()
        {
            var handler = new StubMessageHandler(StubResponse.StartStream());

            using (var es = MakeEventSource(handler))
            {
                _ = Task.Run(es.StartAsync);

                var req = handler.AwaitRequest();
                Assert.False(req.Headers.Contains(Constants.LastEventIdHttpHeader));
            }
        }

        [Fact]
        public void LastEventIdHeaderIsSetIfConfigured()
        {
            var handler = new StubMessageHandler(StubResponse.StartStream());
            var lastEventId = "abc123";

            using (var es = MakeEventSource(handler, builder => builder.LastEventId(lastEventId)))
            {
                _ = Task.Run(es.StartAsync);

                var req = handler.AwaitRequest();
                Assert.True(req.Headers.Contains(Constants.LastEventIdHttpHeader));
                Assert.Contains(lastEventId, req.Headers.GetValues(Constants.LastEventIdHttpHeader));
            }
        }

        [Fact]
        public void CustomRequestHeaders()
        {
            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream());

            var headers = new Dictionary<string, string> { { "User-Agent", "mozilla" }, { "Authorization", "testing" } };

            using (var es = MakeEventSource(handler, builder => builder.RequestHeaders(headers)))
            {
                _ = Task.Run(es.StartAsync);

                var req = handler.AwaitRequest();
                Assert.True(headers.All(
                    item =>
                        req.Headers.Contains(item.Key) &&
                        req.Headers.GetValues(item.Key).Contains(item.Value)
                ));
            }            
        }

        [Fact]
        public void ErrorForIncorrectContentType()
        {
            var handler = new StubMessageHandler();

            var response =
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("testing", System.Text.Encoding.UTF8)
                };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");

            handler.QueueResponse(StubResponse.WithResponse(response));

            using (var es = MakeEventSource(handler))
            {
                var eventSink = new EventSink(es, _testLogging);
                _ = Task.Run(es.StartAsync);

                var errorAction = eventSink.ExpectAction();
                var ex = Assert.IsType<EventSourceServiceCancelledException>(errorAction.Exception);
                Assert.Contains("Content-Type", ex.Message);
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
            var handler = new StubMessageHandler();
            var response = new HttpResponseMessage(statusCode);
            handler.QueueResponse(StubResponse.WithResponse(response));

            using (var es = MakeEventSource(handler))
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
