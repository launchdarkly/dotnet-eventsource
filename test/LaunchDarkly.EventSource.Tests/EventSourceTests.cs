using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Xunit;

namespace LaunchDarkly.EventSource.Tests
{
    public class EventSourceTests
    {
        private Uri uri = new Uri("http://test.com");

        [Fact]
        public async System.Threading.Tasks.Task When_a_comment_SSE_is_received_then_a_comment_event_is_raised()
        {
            // Arrange
            var commentSent = ":";

            var handler = new StubMessageHandler();

            handler.QueueStringResponse(commentSent);

            var config = new Configuration(
             uri: uri,
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
            await evt.Start();

            //// Assert
            Assert.Equal(commentSent, commentReceived);
            Assert.True(wasCommentEventRaised);

        }

        [Fact]
        public async System.Threading.Tasks.Task When_a_data_only_message_SSE_is_received_then_a_message_event_is_raised()
        {
            // Arrange
            var sse = "data: this is a test message\n\n";
            var sseData = "this is a test message";

            var handler = new StubMessageHandler();

            handler.QueueStringResponse(sse);

            var config = new Configuration(
                uri: uri,
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
            await evt.Start();

            //// Assert
            Assert.Equal(sseData, message?.Data);
            Assert.True(wasMessageReceivedEventRaised);

        }

        [Fact]
        public async System.Threading.Tasks.Task When_an_event_message_SSE_is_received_then_a_message_event_is_raised()
        {
            // Arrange
            var sse = "event: put\ndata: this is a test message\n\n";

            var handler = new StubMessageHandler();

            handler.QueueStringResponse(sse);

            var config = new Configuration(
                uri: uri,
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
            await evt.Start();

            //// Assert
            Assert.Equal("put", eventName);
            Assert.True(wasMessageReceivedEventRaised);

        }

        [Fact]
        public async System.Threading.Tasks.Task When_an_message_SSE_contains_id_is_received_then_last_event_id_is_set()
        {
            // Arrange
            var sse = "id:200\nevent: put\ndata: this is a test message\n\n";

            var handler = new StubMessageHandler();

            handler.QueueStringResponse(sse);

            var config = new Configuration(
                uri: uri,
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
            await evt.Start();

            //// Assert
            Assert.Equal("200", message.LastEventId);
            Assert.True(wasMessageReceivedEventRaised);

        }
    }
}
