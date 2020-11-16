using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace LaunchDarkly.EventSource.Tests
{
    public class EventSourceTests
    {
        private readonly Uri _uri = new Uri("http://test.com");

        [Fact]
        public void Can_Create_and_start_EventSource_without_specifying_message_handler()
        {
            // Testing this just because all of the other tests use a StubMessageHandler
            var evt = new EventSource(Configuration.Builder(_uri).Build());
            evt.StartAsync();
        }

        [Fact]
        public async Task When_a_comment_SSE_is_received_then_a_comment_event_is_raised()
        {
            var commentSent = ": hello";

            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream(StreamAction.Write(commentSent + "\n\n")));

            var evt = new EventSource(new Configuration(_uri, handler));

            string commentReceived = null;
            evt.CommentReceived += (_, e) =>
            {
                commentReceived = e.Comment;
                evt.Close();
            };

            await evt.StartAsync();

            Assert.Equal(commentSent, commentReceived);
        }

        [Fact]
        public async Task When_a_data_only_message_SSE_is_received_then_a_message_event_is_raised()
        {
            var sse = "data: this is a test message\n\n";
            var sseData = "this is a test message";

            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream(StreamAction.Write(sse)));

            var evt = new EventSource(new Configuration(_uri, handler));

            var m = new MessageReceiver();
            evt.MessageReceived += m;
            evt.MessageReceived += ((_, e) => evt.Close());
            
            await evt.StartAsync();

            Assert.Equal(sseData, m.RequireSingleEvent().Message.Data);
        }

        [Fact]
        public async Task When_an_event_message_SSE_is_received_then_a_message_event_is_raised()
        {
            var sse = "event: put\ndata: this is a test message\n\n";

            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream(StreamAction.Write(sse)));

            var evt = new EventSource(new Configuration(_uri, handler));

            var m = new MessageReceiver();
            evt.MessageReceived += m;
            evt.MessageReceived += ((_, e) => evt.Close());

            await evt.StartAsync();

            Assert.Equal("put", m.RequireSingleEvent().EventName);
        }

        [Fact]
        public async Task When_an_event_message_SSE_is_received_with_http_client_then_a_message_event_is_raised()
        {
            var sse = "event: httpclient\ndata: this is a test message with httpclient\n\n";

            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream(StreamAction.Write(sse)));

            var client = new HttpClient(handler);
            var evt = new EventSource(new Configuration(_uri, httpClient: client));

            var m = new MessageReceiver();
            evt.MessageReceived += m;
            evt.MessageReceived += ((_, e) => evt.Close());

            await evt.StartAsync();

            Assert.Equal("httpclient", m.RequireSingleEvent().EventName);
            client.Dispose();
        }

        [Fact]
        public async Task When_event_source_closes_do_not_dispose_configured_http_client()
        {
            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.WithResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Hello")}));

            var client = new HttpClient(handler);
            var evt = new EventSource(new Configuration(_uri, httpClient: client));
            evt.Close();

            await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, _uri));
            client.Dispose();
        }

        [Fact]
        public async Task When_an_message_SSE_contains_id_is_received_then_last_event_id_is_set()
        {
            var sse = "id:200\nevent: put\ndata: this is a test message\n\n";

            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream(StreamAction.Write(sse)));

            var evt = new EventSource(new Configuration(_uri, handler));

            var m = new MessageReceiver();
            evt.MessageReceived += m;
            evt.MessageReceived += ((_, e) => evt.Close());

            await evt.StartAsync();

            Assert.Equal("200", m.RequireSingleEvent().Message.LastEventId);
        }

        [Fact]
        public async Task Default_HTTP_method_is_get()
        {
            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream());

            var evt = new EventSource(new Configuration(_uri, handler));
            handler.RequestReceived += (s, r) => evt.Close();
            await evt.StartAsync();

            var request = handler.GetRequests().First();
            Assert.Equal(HttpMethod.Get, request.Method);
        }

        [Fact]
        public async Task HTTP_method_can_be_specified()
        {
            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream());

            var config = new ConfigurationBuilder(_uri).MessageHandler(handler)
                .Method(HttpMethod.Post).Build();
            var evt = new EventSource(config);

            handler.RequestReceived += (s, r) => evt.Close();
            await evt.StartAsync();

            var request = handler.GetRequests().First();
            Assert.Equal(HttpMethod.Post, request.Method);
        }

        [Fact]
        public async Task HTTP_request_body_can_be_specified()
        {
            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream());

            HttpContent content = new StringContent("{}");
            Configuration.HttpContentFactory contentFn = () =>
            {
                return content;
            };

            var config = new ConfigurationBuilder(_uri).MessageHandler(handler)
                .Method(HttpMethod.Post).RequestBodyFactory(contentFn).Build();
            var evt = new EventSource(config);

            handler.RequestReceived += (s, r) => evt.Close();
            await evt.StartAsync();

            var request = handler.GetRequests().First();
            Assert.Equal(content, request.Content);
        }

        [Fact]
        public async Task When_the_HttpRequest_is_sent_then_the_outgoing_request_contains_accept_header()
        {
            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream());
            
            var evt = new EventSource(new Configuration(_uri, handler));
            handler.RequestReceived += (s, r) => evt.Close();

            await evt.StartAsync();

            var request = handler.GetRequests().First();
            Assert.True(request.Headers.Contains(Constants.AcceptHttpHeader));
            Assert.True(request.Headers.GetValues(Constants.AcceptHttpHeader).Contains(Constants.EventStreamContentType));
        }

        [Fact]
        public async Task When_LastEventId_is_configured_then_the_outgoing_request_contains_Last_Event_Id_header()
        {
            var lastEventId = "10";

            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream());

            var config = new ConfigurationBuilder(_uri).MessageHandler(handler)
                .LastEventId(lastEventId).Build();
            var evt = new EventSource(config);
            handler.RequestReceived += (s, r) => evt.Close();

            await evt.StartAsync();

            var request = handler.GetRequests().First();
            Assert.True(request.Headers.Contains(Constants.LastEventIdHttpHeader));
            Assert.True(request.Headers.GetValues(Constants.LastEventIdHttpHeader).Contains(lastEventId));
        }

        [Fact]
        public async Task When_reconnecting_the_outgoing_request_contains_Last_Event_Id_header()
        {
            var lastEventId = "10";
            var firstResponse = $"id:{lastEventId}\nevent: put\ndata: this is a test message\n\n";
            var secondResponse = $"id:20\nevent: put\ndata: this is a test message\n\n";

            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream(StreamAction.Write(firstResponse), StreamAction.CloseStream()));
            handler.QueueResponse(StubResponse.StartStream(StreamAction.Write(secondResponse), StreamAction.CloseStream()));

            var evt = new EventSource(new Configuration(_uri, handler));
            var first = true;
            handler.RequestReceived += (s, r) =>
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    evt.Close();
                }
            };

            await evt.StartAsync();

            var requests = handler.GetRequests().ToList();
            Assert.False(requests[0].Headers.Contains(Constants.LastEventIdHttpHeader));
            Assert.True(requests[1].Headers.Contains(Constants.LastEventIdHttpHeader));
            Assert.True(requests[1].Headers.GetValues(Constants.LastEventIdHttpHeader).Contains(lastEventId));
        }

        [Fact]
        public async Task When_reconnecting_the_outgoing_request_overrides_Last_Event_Id_from_configuration()
        {
            var lastEventId = "10";
            var firstResponse = $"id:{lastEventId}\nevent: put\ndata: this is a test message\n\n";
            var secondResponse = $"id:20\nevent: put\ndata: this is a test message\n\n";

            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream(StreamAction.Write(firstResponse), StreamAction.CloseStream()));
            handler.QueueResponse(StubResponse.StartStream(StreamAction.Write(secondResponse), StreamAction.CloseStream()));

            var configuration = Configuration
                .Builder(_uri)
                .MessageHandler(handler)
                .LastEventId("0")
                .RequestHeader(Constants.LastEventIdHttpHeader, "0")
                .Build();
            var evt = new EventSource(configuration);
            var first = true;
            handler.RequestReceived += (s, r) =>
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    evt.Close();
                }
            };

            await evt.StartAsync();

            var requests = handler.GetRequests().ToList();
            Assert.True(requests[0].Headers.Contains(Constants.LastEventIdHttpHeader));
            Assert.True(requests[1].Headers.Contains(Constants.LastEventIdHttpHeader));
            var lastEventIdHeader = requests[1].Headers.GetValues(Constants.LastEventIdHttpHeader).ToArray();
            Assert.Equal(1, lastEventIdHeader.Length);
            Assert.Equal(lastEventId, lastEventIdHeader[0]);
        }

        [Fact]
        public async Task When_Configuration_Request_headers_are_set_then_the_outgoing_request_contains_those_same_headers()
        {
            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream());

            var headers = new Dictionary<string, string> { { "User-Agent", "mozilla" }, { "Authorization", "testing" } };

            var config = new ConfigurationBuilder(_uri).MessageHandler(handler)
                .RequestHeaders(headers).Build();

            var evt = new EventSource(config);
            handler.RequestReceived += (s, r) => evt.Close();

            await evt.StartAsync();

            var request = handler.GetRequests().First();
            Assert.True(headers.All(
                    item =>
                        request.Headers.Contains(item.Key) &&
                        request.Headers.GetValues(item.Key).Contains(item.Value)
            ));
        }

        [Fact]
        public async Task Given_content_type_not_equal_to_eventstream_when_the_http_response_is_received_then_error_event_should_occur()
        {
            var handler = new StubMessageHandler();

            var response =
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("testing", System.Text.Encoding.UTF8)
                };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");

            handler.QueueResponse(StubResponse.WithResponse(response));

            var config = new Configuration(_uri, handler);

            var evt = new EventSource(config);
            var receiver = new ErrorReceiver();
            evt.Error += receiver;
            evt.Error += (_, e) => evt.Close();

            await evt.StartAsync();
            
            Assert.NotNull(receiver.ErrorReceived);
            Assert.Equal(ReadyState.Closed, receiver.SourceStateReceived);
            Assert.Equal(ReadyState.Shutdown, evt.ReadyState);
        }

        [Fact]
        public async Task Given_204_when_the_http_response_is_received_then_error_event_should_occur()
        {
            var handler = new StubMessageHandler();

            handler.QueueResponse(StubResponse.WithStatus(HttpStatusCode.NoContent));

            var evt = new EventSource(new Configuration(_uri, handler));

            var receiver = new ErrorReceiver();
            evt.Error += receiver;
            evt.Error += (_, e) => evt.Close();

            await evt.StartAsync();

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
            var handler = new StubMessageHandler();

            handler.QueueResponse(StubResponse.WithStatus(statusCode));

            var evt = new EventSource(new Configuration(_uri, handler));

            var receiver = new ErrorReceiver();
            evt.Error += receiver;
            evt.Error += (_, e) => evt.Close();

            await evt.StartAsync();
            
            Assert.NotNull(receiver.ErrorReceived);
            var ex = Assert.IsType<EventSourceServiceUnsuccessfulResponseException>(receiver.ErrorReceived);
            Assert.Equal((int)statusCode, ex.StatusCode);
            Assert.Equal(ReadyState.Closed, receiver.SourceStateReceived);
            Assert.Equal(ReadyState.Shutdown, evt.ReadyState);
        }

        [Fact]
        public async Task Given_bad_http_responses_then_retry_delay_durations_should_increase()
        {
            var handler = new StubMessageHandler();

            var nAttempts = 2;
            for (int i = 0; i < nAttempts; i++)
            {
                handler.QueueResponse(StubResponse.WithIOError());
            }
            handler.QueueResponse(StubResponse.StartStream());

            var evt = new EventSource(new Configuration(_uri, handler));

            var backoffs = new List<TimeSpan>();
            evt.Error += (_, e) =>
            {
                backoffs.Add(evt.BackOffDelay);
                if (backoffs.Count >= nAttempts)
                {
                    evt.Close();
                }
            };
            
            await evt.StartAsync();

            Assert.NotEmpty(backoffs);
            Assert.NotEqual(backoffs[0], backoffs[1]);
            Assert.True(backoffs[1] > backoffs[0]);
        }

        [Fact]
        public async Task When_response_exceeds_read_timeout_then_read_timeout_exception_occurs()
        {
            TimeSpan readTimeout = TimeSpan.FromSeconds(4);
            TimeSpan timeToWait = readTimeout.Add(TimeSpan.FromSeconds(1));

            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream(
                StreamAction.Write(":\n\n").AfterDelay(timeToWait)));
            handler.QueueResponse(StubResponse.StartStream());

            var config = new ConfigurationBuilder(_uri).MessageHandler(handler).ReadTimeout(readTimeout).Build();
            var evt = new EventSource(config);
            
            var receiver = new ErrorReceiver();
            evt.Error += receiver;
            evt.Error += (_, e) => evt.Close();

            await evt.StartAsync();

            Assert.NotNull(receiver.ErrorReceived);
            Assert.Contains(receiver.ErrorReceived.Message, Resources.EventSourceService_Read_Timeout);
            Assert.Equal(ReadyState.Closed, receiver.SourceStateReceived);
            Assert.Equal(ReadyState.Shutdown, evt.ReadyState);
        }

        [Fact]
        public async Task Timeout_does_not_cause_unobserved_exception()
        {
            TimeSpan readTimeout = TimeSpan.FromMilliseconds(10);
            TimeSpan timeToWait = TimeSpan.FromMilliseconds(100);

            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream(
                StreamAction.Write(":\n\n").AfterDelay(timeToWait)));
            handler.QueueResponse(StubResponse.StartStream());

            var config = new ConfigurationBuilder(_uri).MessageHandler(handler).ReadTimeout(readTimeout).Build();
            var evt = new EventSource(config);

            var caughtUnobservedException = false;
            EventHandler<UnobservedTaskExceptionEventArgs> exceptionHandler = (object sender, UnobservedTaskExceptionEventArgs e) =>
            {
                e.SetObserved();
                caughtUnobservedException = true;
            };
            TaskScheduler.UnobservedTaskException += exceptionHandler;

            try
            {
                evt.Error += (_, e) => evt.Close();

                await evt.StartAsync();

                // StartAsync has returned, meaning that the EventSource was closed by the ErrorReceiver, meaning that it
                // encountered a timeout. Wait a little bit longer to make sure that the stream reader task has got an
                // exception from the closed stream.
                Thread.Sleep(TimeSpan.FromMilliseconds(300));

                // Force finalizer to run so that if there was an unobserved exception, it will trigger that event.
                GC.Collect();
                GC.WaitForPendingFinalizers();

                Assert.False(caughtUnobservedException);
            }
            finally
            {
                TaskScheduler.UnobservedTaskException -= exceptionHandler;
            }
        }

        [Fact]
        public async Task When_response_does_not_exceed_read_timeout_then_expected_message_event_occurs()
        {
            var sse = "event: put\ndata: this is a test message\n\n";
            TimeSpan readTimeout = TimeSpan.FromSeconds(4);
            TimeSpan timeToWait = readTimeout.Subtract(TimeSpan.FromSeconds(1));

            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.StartStream(
                StreamAction.Write(sse).AfterDelay(timeToWait)));

            var config = new ConfigurationBuilder(_uri).MessageHandler(handler).ReadTimeout(readTimeout).Build();
            var evt = new EventSource(config);

            var receiver = new MessageReceiver();
            evt.MessageReceived += receiver;
            evt.MessageReceived += (_, e) => evt.Close();

            await evt.StartAsync();

            Assert.Equal("put", receiver.RequireSingleEvent().EventName);
        }

        [Fact]
        public async Task When_server_returns_HTTP_error_a_reconnect_attempt_is_made()
        {
            var messageData = "hello";

            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.WithStatus(HttpStatusCode.Unauthorized));
            handler.QueueResponse(StubResponse.StartStream(
                StreamAction.Write("event: put\ndata: " + messageData + "\n\n")));

            var evt = new EventSource(new Configuration(_uri, handler));

            var errorReceiver = new ErrorReceiver();
            evt.Error += errorReceiver;

            var messageReceiver = new MessageReceiver();
            evt.MessageReceived += messageReceiver;
            evt.MessageReceived += (_, e) => evt.Close();

            await evt.StartAsync();

            Assert.Equal(2, handler.GetRequests().Count());
            Assert.NotNull(errorReceiver.ErrorReceived);
            var ex = Assert.IsType<EventSourceServiceUnsuccessfulResponseException>(errorReceiver.ErrorReceived);
            Assert.Equal((int)HttpStatusCode.Unauthorized, ex.StatusCode);
            Assert.Equal(messageData, messageReceiver.RequireSingleEvent().Message.Data);
        }

        [Fact]
        public async Task When_error_handler_closes_event_source_no_reconnect_attempt_is_made()
        {
            var handler = new StubMessageHandler();
            handler.QueueResponse(StubResponse.WithStatus(HttpStatusCode.Unauthorized));
            handler.QueueResponse(StubResponse.StartStream());

            var evt = new EventSource(new Configuration(_uri, handler));

            evt.Error += (_, e) => evt.Close();

            await evt.StartAsync();

            Assert.Equal(1, handler.GetRequests().Count());
        }

        public async Task Connection_closed_before_threshold_gets_increasing_backoff_delay()
        {
            TimeSpan threshold = TimeSpan.FromSeconds(1);
            TimeSpan closeDelay = TimeSpan.FromMilliseconds(100);
            const int nAttempts = 3;

            var handler = new StubMessageHandler();
            for (var i = 0; i < nAttempts; i++)
            {
                handler.QueueResponse(StubResponse.StartStream(
                    StreamAction.CloseStream().AfterDelay(closeDelay)));
            }
            handler.QueueResponse(StubResponse.StartStream());
            
            var config = new ConfigurationBuilder(_uri).MessageHandler(handler)
                .BackoffResetThreshold(threshold).Build();
            var evt = new EventSource(config);

            var requestTimes = new List<DateTime>();
            handler.RequestReceived += (_, r) =>
            {
                requestTimes.Add(DateTime.Now);
                if (requestTimes.Count > nAttempts)
                {
                    evt.Close();
                }
            };
            
            await evt.StartAsync();

            Assert.Equal(nAttempts + 1, handler.GetRequests().Count());

            for (var i = 0; i < nAttempts; i++)
            {
                var interval = requestTimes[i + 1] - requestTimes[i] - closeDelay;
                var min = (i == 0) ? TimeSpan.Zero : GetMaxBackoffDelayForAttempt(config, i);
                var max = GetMaxBackoffDelayForAttempt(config, i + 1);
                AssertTimeSpanInRange(interval, min, max);
            }
        }

        public async Task Connection_closed_after_threshold_does_not_get_increasing_backoff_delay()
        {
            TimeSpan threshold = TimeSpan.FromMilliseconds(10);
            TimeSpan closeDelay = TimeSpan.FromMilliseconds(100);
            const int nAttempts = 3;

            var handler = new StubMessageHandler();
            for (var i = 0; i < nAttempts; i++)
            {
                handler.QueueResponse(StubResponse.StartStream(
                    StreamAction.CloseStream().AfterDelay(closeDelay)));
            }
            handler.QueueResponse(StubResponse.StartStream());

            var config = new ConfigurationBuilder(_uri).MessageHandler(handler)
                .BackoffResetThreshold(threshold).Build();
            var evt = new EventSource(config);

            var requestTimes = new List<DateTime>();
            handler.RequestReceived += (_, r) =>
            {
                requestTimes.Add(DateTime.Now);
                if (requestTimes.Count == 3)
                {
                    evt.Close();
                }
            };

            await evt.StartAsync();

            var max = GetMaxBackoffDelayForAttempt(config, 1);
            for (var i = 0; i < nAttempts; i++)
            {
                var interval = requestTimes[i + 1] - requestTimes[i] - closeDelay;
                AssertTimeSpanInRange(interval, TimeSpan.Zero, max);
            }
        }

        TimeSpan GetMaxBackoffDelayForAttempt(Configuration config, int attempt)
        {
            var backoff = new ExponentialBackoffWithDecorrelation(config.DelayRetryDuration,
                Configuration.MaximumRetryDuration);
            return TimeSpan.FromMilliseconds(backoff.GetMaximumMillisecondsForAttempt(attempt));
        }

        void AssertTimeSpanInRange(TimeSpan t, TimeSpan min, TimeSpan max)
        {
            Assert.True(t >= min, "TimeSpan of " + t + " should have been >= " + min);
            Assert.True(t <= max, "TimeSpan of " + t + " should have been <= " + max);
        }
    }

    class ErrorReceiver
    {
        public Exception ErrorReceived = null;
        public ReadyState SourceStateReceived;
        
        public static implicit operator EventHandler<ExceptionEventArgs>(ErrorReceiver r)
        {
            return r.OnError;
        }

        public void OnError(object sender, ExceptionEventArgs e)
        {
            ErrorReceived = e.Exception;
            SourceStateReceived = (sender as EventSource).ReadyState;
        }
    }

    class MessageReceiver
    {
        public List<MessageReceivedEventArgs> Events = new List<MessageReceivedEventArgs>();

        private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            Events.Add(e);
        }
    
        public static implicit operator EventHandler<MessageReceivedEventArgs>(MessageReceiver r)
        {
            return r.OnMessageReceived;
        }

        public MessageReceivedEventArgs RequireSingleEvent()
        {
            Assert.Equal(1, Events.Count);
            return Events[0];
        }
    }
}
