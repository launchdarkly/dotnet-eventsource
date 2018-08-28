using LaunchDarkly.EventSource.Tests.Stubs;
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
        private readonly TimeSpan _defaultReadTimeout = TimeSpan.FromSeconds(1);

        [Fact]
        public void Exponential_backoff_should_not_exceed_maximum()
        {
            TimeSpan max = TimeSpan.FromMilliseconds(30000);
            ExponentialBackoffWithDecorrelation expo =
                new ExponentialBackoffWithDecorrelation(TimeSpan.FromMilliseconds(1000), max);

            var backoff = expo.GetNextBackOff();

            Assert.True(backoff <= max);
        }

        [Fact]
        public void Exponential_backoff_should_not_exceed_maximum_in_test_loop()
        {
            TimeSpan max = TimeSpan.FromMilliseconds(30000);

            ExponentialBackoffWithDecorrelation expo =
                new ExponentialBackoffWithDecorrelation(TimeSpan.FromMilliseconds(1000), max);

            for (int i = 0; i < 100; i++)
            {

                var backoff = expo.GetNextBackOff();

                Assert.True(backoff <= max);
            }

        }

        [Fact]
        public void Exponential_backoff_should_reset_when_reconnect_count_resets()
        {
            TimeSpan max = TimeSpan.FromMilliseconds(30000);

            ExponentialBackoffWithDecorrelation expo =
                new ExponentialBackoffWithDecorrelation(TimeSpan.FromMilliseconds(1000), max);

            for (int i = 0; i < 100; i++)
            {
                var backoff = expo.GetNextBackOff();
            }
            expo.ResetReconnectAttemptCount();
            // Backoffs use jitter, so assert that the reset backoff time isn't more than double the minimum
            Assert.True(expo.GetNextBackOff() <= TimeSpan.FromMilliseconds(2000));
        }

        [Fact]
        public async Task When_a_comment_SSE_is_received_then_a_comment_event_is_raised()
        {
            // Arrange
            var commentSent = ":";

            var handler = new StubMessageHandler();

            handler.QueueStringResponse(commentSent);

            var evt = new EventSource(new Configuration(_uri, handler, readTimeout:_defaultReadTimeout));

            string commentReceived = string.Empty;
            var wasCommentEventRaised = false;
            evt.CommentReceived += (_, e) =>
            {
                commentReceived = e.Comment;
                wasCommentEventRaised = true;

                evt.Close();
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

            var evt = new EventSource(new Configuration(_uri, handler, readTimeout:_defaultReadTimeout));

            MessageEvent message = null;
            var wasMessageReceivedEventRaised = false;
            evt.MessageReceived += (_, e) =>
            {
                message = e.Message;
                wasMessageReceivedEventRaised = true;

                evt.Close();
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

            var evt = new EventSource(new Configuration(_uri, handler, readTimeout:_defaultReadTimeout));

            var wasMessageReceivedEventRaised = false;
            var eventName = "message";
            evt.MessageReceived += (_, e) =>
            {
                eventName = e.EventName;
                wasMessageReceivedEventRaised = true;

                evt.Close();
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

            var evt = new EventSource(new Configuration(_uri, handler, readTimeout:_defaultReadTimeout));

            MessageEvent message = null;
            var wasMessageReceivedEventRaised = false;
            evt.MessageReceived += (_, e) =>
            {
                message = e.Message;
                wasMessageReceivedEventRaised = true;

                evt.Close();
            };

            //// Act
            await evt.StartAsync();

            //// Assert
            Assert.Equal("200", message.LastEventId);
            Assert.True(wasMessageReceivedEventRaised);

        }

        [Fact]
        public async Task Default_HTTP_method_is_get()
        {
            var handler = new StubMessageHandler();
            handler.QueueStringResponse(":");

            var evt = new EventSource(new Configuration(_uri, handler, readTimeout: _defaultReadTimeout));
            evt.CommentReceived += (_, e) => { evt.Close(); };
            await evt.StartAsync();

            var request = handler.GetRequests().First();
            Assert.Equal(HttpMethod.Get, request.Method);
        }

        [Fact]
        public async Task HTTP_method_can_be_specified()
        {
            var handler = new StubMessageHandler();
            handler.QueueStringResponse(":");

            var evt = new EventSource(new Configuration(_uri, handler, readTimeout: _defaultReadTimeout,
                method: HttpMethod.Post));
            evt.CommentReceived += (_, e) => { evt.Close(); };
            await evt.StartAsync();

            var request = handler.GetRequests().First();
            Assert.Equal(HttpMethod.Post, request.Method);
        }

        [Fact]
        public async Task HTTP_request_body_can_be_specified()
        {
            var handler = new StubMessageHandler();
            handler.QueueStringResponse(":");

            HttpContent content = new StringContent("{}");
            Configuration.HttpContentFactory contentFn = () =>
            {
                return content;
            };

            var evt = new EventSource(new Configuration(_uri, handler, readTimeout: _defaultReadTimeout,
                method: HttpMethod.Post, requestBodyFactory: contentFn));
            evt.CommentReceived += (_, e) => { evt.Close(); };
            await evt.StartAsync();

            var request = handler.GetRequests().First();
            Assert.Equal(content, request.Content);
        }

        [Fact]
        public async Task When_the_HttpRequest_is_sent_then_the_outgoing_request_contains_accept_header()
        {
            // Arrange
            var sse = ":";

            var handler = new StubMessageHandler();

            handler.QueueStringResponse(sse);

            var evt = new EventSource(new Configuration(_uri, handler, readTimeout:_defaultReadTimeout));
            evt.CommentReceived += (_, e) =>
            {
                evt.Close();
            };

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
                readTimeout:_defaultReadTimeout,
                lastEventId: lastEventId);

            var evt = new EventSource(config);

            evt.CommentReceived += (_, e) =>
            {
                evt.Close();
            };

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

            var config = new Configuration(_uri, handler, requestHeaders: headers, readTimeout:_defaultReadTimeout);

            var evt = new EventSource(config);
            evt.CommentReceived += (_, e) =>
            {
                evt.Close();
            };

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
        public async Task Given_content_type_not_equal_to_eventstream_when_the_http_response_is_received_then_error_event_should_occur()
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

            var config = new Configuration( _uri, handler);

            var evt = new EventSource(config);

            var receiver = new ErrorReceiver(evt);

            //// Act
            await evt.StartAsync();

            //// Assert
            Assert.NotNull(receiver.ErrorReceived);
            Assert.Equal(ReadyState.Closed, receiver.SourceStateReceived);
            Assert.Equal(ReadyState.Shutdown, evt.ReadyState);
        }

        [Fact]
        public async Task Given_204_when_the_http_response_is_received_then_error_event_should_occur()
        {
            // Arrange
            var handler = new StubMessageHandler();

            handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.NoContent));

            var evt = new EventSource(new Configuration(_uri, handler));

            var receiver = new ErrorReceiver(evt);

            //Act
            await evt.StartAsync();

            //// Assert
            Assert.NotNull(receiver.ErrorReceived);
            var ex = Assert.IsType<EventSourceServiceUnsuccessfulResponseException>(receiver.ErrorReceived);
            Assert.Equal(204, ex.StatusCode);
            Assert.Equal(ReadyState.Closed, receiver.SourceStateReceived);
            Assert.Equal(ReadyState.Shutdown, evt.ReadyState);
        }

        [Theory]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.RequestTimeout)]
        [InlineData(HttpStatusCode.Unauthorized)]
        public async Task Given_status_code_when_the_http_response_is_received_then_error_event_should_occur(HttpStatusCode statusCode)
        {
            // Arrange
            var handler = new StubMessageHandler();

            handler.QueueResponse(new HttpResponseMessage(statusCode));

            var evt = new EventSource(new Configuration(_uri, handler));

            ErrorReceiver receiver = new ErrorReceiver(evt);

            await evt.StartAsync();

            //// Assert
            Assert.NotNull(receiver.ErrorReceived);
            var ex = Assert.IsType<EventSourceServiceUnsuccessfulResponseException>(receiver.ErrorReceived);
            Assert.Equal((int)statusCode, ex.StatusCode);
            Assert.Equal(ReadyState.Closed, receiver.SourceStateReceived);
            Assert.Equal(ReadyState.Shutdown, evt.ReadyState);
        }

        [Fact]
        public async Task Given_bad_http_responses_then_retry_delay_durations_should_be_random()
        {
            // Arrange
            var handler = new StubMessageHandler();

            var nAttempts = 2;
            for (int i = 0; i < nAttempts; i++)
            {
                var response = new HttpResponseMessageWithError();

                response.StatusCode = HttpStatusCode.OK;
                response.ShouldThrowError = true;
                response.Content = new StringContent("Content " + i, System.Text.Encoding.UTF8,
                    "text/event-stream");

                handler.QueueResponse(response);
            }

            handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.NoContent));

            var evt = new EventSource(new Configuration(_uri, handler, readTimeout:_defaultReadTimeout));

            var backoffs = new List<TimeSpan>();
            evt.Error += (_, e) =>
            {
                backoffs.Add(evt.BackOffDelay);
                if (backoffs.Count >= nAttempts)
                {
                    evt.Close();
                }
            };

            //Act
            await evt.StartAsync();

            //// Assert
            Assert.NotEmpty(backoffs);
            Assert.Equal(backoffs.Distinct().Count(), backoffs.Count());
        }


        [Fact]
        public async Task When_response_exceeds_read_timeout_then_read_timeout_exception_occurs()
        {
            var commentSent = ":";

            var handler = new StubMessageHandler();
            handler.QueueStringResponse(commentSent);

            TimeSpan readTimeout = TimeSpan.FromSeconds(4);
            TimeSpan timeout = readTimeout.Add(TimeSpan.FromSeconds(1));

            var evt = new StubEventSource(new Configuration(_uri, handler, readTimeout: readTimeout), (int)timeout.TotalMilliseconds);

            var receiver = new ErrorReceiver(evt);

            await evt.StartAsync();

            Assert.NotNull(receiver.ErrorReceived);
            Assert.Contains(receiver.ErrorReceived.Message, Resources.EventSourceService_Read_Timeout);
            Assert.Equal(ReadyState.Closed, receiver.SourceStateReceived);
            Assert.Equal(ReadyState.Shutdown, evt.ReadyState);
        }

        [Fact]
        public async Task When_response_does_not_exceed_read_timeout_then_expected_message_event_occurs()
        {
            var sse = "event: put\ndata: this is a test message\n\n";

            var handler = new StubMessageHandler();
            handler.QueueStringResponse(sse);
            handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.NoContent));

            TimeSpan readTimeout = TimeSpan.FromSeconds(4);
            TimeSpan timeout = readTimeout.Subtract(TimeSpan.FromSeconds(1));

            var evt = new StubEventSource(new Configuration(_uri, handler, readTimeout: readTimeout), (int)timeout.TotalMilliseconds);

            var wasMessageReceivedEventRaised = false;
            var eventName = "message";
            evt.MessageReceived += (_, e) =>
            {
                eventName = e.EventName;
                wasMessageReceivedEventRaised = true;

                evt.Close();
            };

            await evt.StartAsync();

            Assert.Equal("put", eventName);
            Assert.True(wasMessageReceivedEventRaised);
        }

        [Fact]
        public async Task When_server_returns_HTTP_error_a_reconnect_attempt_is_made()
        {
            var messageData = "hello";

            var handler = new StubMessageHandler();
            handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            handler.QueueStringResponse("event: put\ndata: " + messageData + "\n\n");

            var evt = new EventSource(new Configuration(_uri, handler));

            ErrorReceiver receiver = new ErrorReceiver(evt);
            receiver.CloseEventSourceOnError = false;

            string messageReceived = null;
            evt.MessageReceived += (_, e) =>
            {
                messageReceived = e.Message.Data;
                evt.Close();
            };

            await evt.StartAsync();

            Assert.Equal(2, handler.GetRequests().Count());
            Assert.NotNull(receiver.ErrorReceived);
            var ex = Assert.IsType<EventSourceServiceUnsuccessfulResponseException>(receiver.ErrorReceived);
            Assert.Equal((int)HttpStatusCode.Unauthorized, ex.StatusCode);
            Assert.Equal(messageData, messageReceived);
        }

        [Fact]
        public async Task When_error_handler_closes_event_source_no_reconnect_attempt_is_made()
        {
            var handler = new StubMessageHandler();
            handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.Unauthorized));

            var evt = new EventSource(new Configuration(_uri, handler));

            var receiver = new ErrorReceiver(evt);

            await evt.StartAsync();

            Assert.Equal(1, handler.GetRequests().Count());
        }
    }

    class ErrorReceiver
    {
        public bool CloseEventSourceOnError = true;
        public Exception ErrorReceived = null;
        public ReadyState SourceStateReceived;
        private readonly EventSource _source;
        
        public ErrorReceiver(EventSource source)
        {
            _source = source;
            source.Error += HandleError;
        }

        public void HandleError(object sender, ExceptionEventArgs e)
        {
            ErrorReceived = e.Exception;
            SourceStateReceived = _source.ReadyState;
            if (CloseEventSourceOnError)
            {
                _source.Close();
            }
        }
    }
}
