using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LaunchDarkly.EventSource.Tests
{
    public class StubMessageHandler : HttpMessageHandler
    {
        private readonly Queue<StubResponse> _responses = new Queue<StubResponse>();

        // Requests that were sent via the handler
        private readonly List<HttpRequestMessage> _requests =
            new List<HttpRequestMessage>();

        public event EventHandler<HttpRequestMessage> RequestReceived;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
                throw new InvalidOperationException("No response configured");

            RequestReceived?.Invoke(this, request);

            _requests.Add(request);

            var response = _responses.Dequeue();

            if (response.IoError)
            {
                throw new HttpRequestException("Unit Test Exception Message");
            }
 
            if (response.Response != null)
            {
                return Task.FromResult(response.Response);
            }

            var httpResponse = new HttpResponseMessage(response.Status);
            var streamRead = new AnonymousPipeServerStream(PipeDirection.In);
            var streamWrite = new AnonymousPipeClientStream(PipeDirection.Out, streamRead.ClientSafePipeHandle);
            var content = new StreamContent(streamRead);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            httpResponse.Content = content;

            Task.Run(() => WriteStreamingResponse(response, streamWrite, cancellationToken));

            return Task.FromResult(httpResponse);
        }

        public void QueueResponse(StubResponse response) => _responses.Enqueue(response);
        
        public IEnumerable<HttpRequestMessage> GetRequests() =>
            _requests;
        
        private async Task WriteStreamingResponse(StubResponse response, Stream output, CancellationToken cancellationToken)
        {
            try
            {
                foreach (var action in response.Actions)
                {
                    if (action.Delay != TimeSpan.Zero)
                    {
                        await Task.Delay(action.Delay, cancellationToken);
                    }
                    if (action.Content == null)
                    {
                        return;
                    }
                    byte[] data = Encoding.UTF8.GetBytes(action.Content);
                    await output.WriteAsync(data, 0, data.Length, cancellationToken);
                }
                // if we've run out of actions, leave the stream open until it's cancelled
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
            }
            catch (Exception)
            {
                System.Console.WriteLine("yo");
                // just exit
            }
        }
    }

    public class StubResponse
    {
        internal readonly bool IoError;
        internal readonly HttpStatusCode Status;
        internal readonly HttpResponseMessage Response;
        internal readonly List<StubStreamAction> Actions;

        private StubResponse(bool ioError, HttpStatusCode status, HttpResponseMessage response)
        {
            IoError = ioError;
            Status = status;
            Response = response;
            Actions = new List<StubStreamAction>();
        }

        public static StubResponse WithIoError()
        {
            return new StubResponse(true, 0, null);
        }

        public static StubResponse WithStatus(HttpStatusCode status)
        {
            return new StubResponse(false, status, null);
        }

        public static StubResponse Ok()
        {
            return WithStatus(HttpStatusCode.OK);
        }

        public static StubResponse WithResponse(HttpResponseMessage response)
        {
            return new StubResponse(false, HttpStatusCode.OK, response);
        }

        public StubResponse WithAction(StubStreamAction action)
        {
            Actions.Add(action);
            return this;
        }
    }

    public class StubStreamAction
    {
        internal readonly TimeSpan Delay;
        internal readonly string Content;

        private StubStreamAction(TimeSpan delay, string content)
        {
            Delay = delay;
            Content = content;
        }

        public static StubStreamAction Write(string content)
        {
            return new StubStreamAction(TimeSpan.Zero, content);
        }

        public static StubStreamAction CloseStream()
        {
            return new StubStreamAction(TimeSpan.Zero, null);
        }

        public StubStreamAction AfterDelay(TimeSpan delay)
        {
            return new StubStreamAction(delay, Content);
        }
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
