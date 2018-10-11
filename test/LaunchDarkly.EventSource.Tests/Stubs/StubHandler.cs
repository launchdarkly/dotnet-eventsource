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
            return Task.FromResult(response.MakeResponse(cancellationToken));
        }

        public void QueueResponse(StubResponse response) => _responses.Enqueue(response);
        
        public IEnumerable<HttpRequestMessage> GetRequests() =>
            _requests;
    }

    public abstract class StubResponse
    {
        public abstract HttpResponseMessage MakeResponse(CancellationToken cancellationToken);

        protected StubResponse() { }

        public static StubResponse WithResponse(HttpResponseMessage message)
        {
            return new StubResponseWithHttpResponse(message);
        }

        public static StubResponse WithStatus(HttpStatusCode status)
        {
            return new StubResponseWithHttpResponse(new HttpResponseMessage(status));
        }

        public static StubResponse WithIOError()
        {
            return new StubResponseWithIOError();
        }

        public static StubResponse StartStream(params StreamAction[] actions)
        {
            return new StubResponseWithStream(actions);
        }
    }

    internal class StubResponseWithHttpResponse : StubResponse
    {
        private readonly HttpResponseMessage message;

        public StubResponseWithHttpResponse(HttpResponseMessage message)
        {
            this.message = message;
        }

        override public HttpResponseMessage MakeResponse(CancellationToken cancellationToken)
        {
            return message;
        }
    }

    internal class StubResponseWithIOError : StubResponse
    {
        override public HttpResponseMessage MakeResponse(CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Unit Test Exception Message");
        }
    }

    internal class StubResponseWithStream : StubResponse
    {
        private readonly StreamAction[] _actions;

        public StubResponseWithStream(StreamAction[] actions)
        {
            _actions = actions;
        }

        override public HttpResponseMessage MakeResponse(CancellationToken cancellationToken)
        {
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
            var streamRead = new AnonymousPipeServerStream(PipeDirection.In);
            var streamWrite = new AnonymousPipeClientStream(PipeDirection.Out, streamRead.ClientSafePipeHandle);
            var content = new StreamContent(streamRead);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            httpResponse.Content = content;

            Task.Run(() => WriteStreamingResponse(streamWrite, cancellationToken));

            return httpResponse;
        }
        
        private async Task WriteStreamingResponse(Stream output, CancellationToken cancellationToken)
        {
            try
            {
                foreach (var action in _actions)
                {
                    if (action.Delay != TimeSpan.Zero)
                    {
                        await Task.Delay(action.Delay, cancellationToken);
                    }
                    if (action.ShouldQuit())
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
    
    public class StreamAction
    {
        internal readonly TimeSpan Delay;
        internal readonly string Content;

        private StreamAction(TimeSpan delay, string content)
        {
            Delay = delay;
            Content = content;
        }

        public bool ShouldQuit()
        {
            return Content == null;
        }

        public static StreamAction Write(string content)
        {
            return new StreamAction(TimeSpan.Zero, content);
        }

        public static StreamAction CloseStream()
        {
            return new StreamAction(TimeSpan.Zero, null);
        }

        public StreamAction AfterDelay(TimeSpan delay)
        {
            return new StreamAction(delay, Content);
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
