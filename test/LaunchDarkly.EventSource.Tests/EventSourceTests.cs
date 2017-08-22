using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Xunit;

namespace LaunchDarkly.EventSource.Tests
{
    public class EventSourceTests
    {
        private readonly Uri _uri = new Uri("http://test.com");

        [Fact]
        public async Task When_a_comment_SSE_is_received_then_a_comment_event_is_raised()
        {
            // Arrange
            var commentSent = ":";

            var handler = new StubMessageHandler();

            handler.QueueStringResponse(commentSent);

            var config = new Configuration(
             uri: _uri,
             messageHandler: handler);

            var evt = new EventSource(config);

            string commentReceived = string.Empty;
            var wasCommentEventRaised = false;
            evt.CommentReceived += (_, e) =>
            {
                commentReceived = e.Comment;
                wasCommentEventRaised = true;
            };

            //// Act
            await evt.StartAsync();

            //// Assert
            Assert.Equal(commentSent, commentReceived);
            Assert.True(wasCommentEventRaised);

        }

        [Fact]
        public async Task When_a_data_only_message_SSE_is_received_then_a_message_event_is_raised()
        {
            // Arrange
            var sse = "data: this is a test message\n\n";
            var sseData = "this is a test message";

            var handler = new StubMessageHandler();

            handler.QueueStringResponse(sse);

            var config = new Configuration(
                uri: _uri,
                messageHandler: handler);

            var evt = new EventSource(config);

            MessageEvent message = null;
            var wasMessageReceivedEventRaised = false;
            evt.MessageReceived += (_, e) =>
            {
                message = e.Message;
                wasMessageReceivedEventRaised = true;
            };

            //// Act
            await evt.StartAsync();

            //// Assert
            Assert.Equal(sseData, message?.Data);
            Assert.True(wasMessageReceivedEventRaised);

        }

        [Fact]
        public async Task When_an_event_message_SSE_is_received_then_a_message_event_is_raised()
        {
            // Arrange
            var sse = "event: put\ndata: this is a test message\n\n";

            var handler = new StubMessageHandler();

            handler.QueueStringResponse(sse);

            var config = new Configuration(
                uri: _uri,
                messageHandler: handler);

            var evt = new EventSource(config);

            var wasMessageReceivedEventRaised = false;
            var eventName = "message";
            evt.MessageReceived += (_, e) =>
            {
                eventName = e.EventName;
                wasMessageReceivedEventRaised = true;
            };

            //// Act
            await evt.StartAsync();

            //// Assert
            Assert.Equal("put", eventName);
            Assert.True(wasMessageReceivedEventRaised);

        }

        [Fact]
        public async Task When_an_message_SSE_contains_id_is_received_then_last_event_id_is_set()
        {
            // Arrange
            var sse = "id:200\nevent: put\ndata: this is a test message\n\n";

            var handler = new StubMessageHandler();

            handler.QueueStringResponse(sse);

            var config = new Configuration(
                uri: _uri,
                messageHandler: handler);

            var evt = new EventSource(config);

            MessageEvent message = null;
            var wasMessageReceivedEventRaised = false;
            evt.MessageReceived += (_, e) =>
            {
                message = e.Message;
                wasMessageReceivedEventRaised = true;
            };

            //// Act
            await evt.StartAsync();

            //// Assert
            Assert.Equal("200", message.LastEventId);
            Assert.True(wasMessageReceivedEventRaised);

        }

        [Fact]
        public async Task When_the_HttpRequest_is_sent_then_the_outgoing_request_contains_accept_header()
        {
            // Arrange
            var sse = ":";

            var handler = new StubMessageHandler();

            handler.QueueStringResponse(sse);

            var config = new Configuration(
                uri: _uri,
                messageHandler: handler);

            var evt = new EventSource(config);

            //// Act
            await evt.StartAsync();

            var request = handler.GetRequests().First();

            IEnumerable<string> headerValues;
            var acceptHeaderExists = request.Headers.TryGetValues(Constants.AcceptHttpHeader, out headerValues);

            //// Assert
            Assert.True(acceptHeaderExists);
            Assert.True(headerValues.Contains(Constants.EventStreamContentType));

        }

        [Fact]
        public async Task When_LastEventId_is_configured_then_the_outgoing_request_contains_Last_Event_Id_header()
        {
            // Arrange
            var sse = ":";
            var lastEventId = "10";

            var handler = new StubMessageHandler();

            handler.QueueStringResponse(sse);

            var config = new Configuration(
                uri: _uri,
                messageHandler: handler,
                lastEventId: lastEventId);

            var evt = new EventSource(config);

            //// Act
            await evt.StartAsync();
            var request = handler.GetRequests().First();

            IEnumerable<string> headerValues;
            var lastEventHeaderExists = request.Headers.TryGetValues(Constants.LastEventIdHttpHeader, out headerValues);

            //// Assert
            Assert.True(lastEventHeaderExists);
            Assert.Equal(lastEventId, headerValues.First());
        }

        [Fact]
        public async Task When_Configuration_Request_headers_are_set_then_the_outgoing_request_contains_those_same_headers()
        {
            // Arrange
            var sse = ":";

            var handler = new StubMessageHandler();
            handler.QueueStringResponse(sse);

            var headers = new Dictionary<string, string> { { "User-Agent", "mozilla" }, { "Authorization", "testing" } };

            var config = new Configuration(uri: _uri, messageHandler: handler, requestHeaders: headers);

            var evt = new EventSource(config);

            //// Act
            await evt.StartAsync();

            var request = handler.GetRequests().First();

            //// Assert
            Assert.True(headers.All(
                    item =>

                        request.Headers.Contains(item.Key) &&
                        request.Headers.GetValues(item.Key).Contains(item.Value)
            ));
        }

        [Fact]
        public async Task Given_content_type_not_equal_to_eventstream_when_the_http_response_is_recieved_then_error_event_should_occur()
        {
            // Arrange
            var handler = new StubMessageHandler();

            var response =
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("testing", System.Text.Encoding.UTF8)
                };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");

            handler.QueueResponse(response);

            var config = new Configuration(uri: _uri, messageHandler: handler);

            var evt = new EventSource(config);

            var wasErrorEventRaised = false;
            evt.Error += (s, e) =>
            {
                wasErrorEventRaised = true;
            };

            //// Act
            await evt.StartAsync();

            //// Assert
            Assert.True(wasErrorEventRaised);
            Assert.True(evt.ReadyState == ReadyState.Closed);
        }

        [Fact]
        public async Task Given_204_when_the_http_response_is_recieved_then_error_event_should_occur()
        {
            // Arrange
            var handler = new StubMessageHandler();

            handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.NoContent));

            var eventSource = new EventSource(new Configuration(_uri, handler));

            //// Act
            var raisedEvent = await Assert.RaisesAsync<ExceptionEventArgs>(
                h => eventSource.Error += h,
                h => eventSource.Error -= h,
                () => eventSource.StartAsync());

            //// Assert
            Assert.NotNull(raisedEvent);
            Assert.Equal(eventSource, raisedEvent.Sender);
            Assert.IsType<OperationCanceledException>(raisedEvent.Arguments.Exception);
            Assert.True(eventSource.ReadyState == ReadyState.Closed);
        }

    }
}
