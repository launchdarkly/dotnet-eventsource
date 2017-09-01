﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LaunchDarkly.EventSource.Tests
{
    public class StubMessageHandler : HttpMessageHandler
    {

        // Responses to return
        private readonly Queue<HttpResponseMessage> _responses =
            new Queue<HttpResponseMessage>();

        // Requests that were sent via the handler
        private readonly List<HttpRequestMessage> _requests =
            new List<HttpRequestMessage>();


        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
                throw new InvalidOperationException("No response configured");

            _requests.Add(request);


            var response = _responses.Dequeue();

            /* Custom Response Message class provides the ability to throw an exception
              This is useful for testing the retry capabilities. 
            */ 
            var responseMessageWithError = response as HttpResponseMessageWithError;
            if (responseMessageWithError != null)
            {
                if (responseMessageWithError.ShouldThrowError)
                    throw new HttpRequestException("Unit Test Exception Message");
            }
            
            return Task.FromResult(response);
        }


        public void QueueResponse(HttpResponseMessage response) =>
            _responses.Enqueue(response);

        public void QueueStringResponse(string content)
        {
            var response =
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content, System.Text.Encoding.UTF8, "text/event-stream")
                };

            _responses.Enqueue(response);
        }

        public IEnumerable<HttpRequestMessage> GetRequests() =>
            _requests;
    }

    public class HttpResponseMessageWithError : HttpResponseMessage
    {
        public bool ShouldThrowError
        {
            get;
            set;
        }
    }
}
