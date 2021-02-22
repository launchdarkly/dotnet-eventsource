using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace LaunchDarkly.EventSource.Tests
{
    public class StubMessageHandler : HttpMessageHandler
    {
        private readonly Queue<StubResponse> _responses = new Queue<StubResponse>();

        // Requests that were sent via the handler
        private readonly BlockingCollection<HttpRequestMessage> _requests =
            new BlockingCollection<HttpRequestMessage>();

        public StubMessageHandler() { }

        public StubMessageHandler(params StubResponse[] resps)
        {
            foreach (var resp in resps)
            {
                _responses.Enqueue(resp);
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
                throw new InvalidOperationException("No response configured");

            _requests.Add(request);

            var response = _responses.Dequeue();
            return Task.FromResult(response.MakeResponse(cancellationToken));
        }

        public void QueueResponse(StubResponse response) => _responses.Enqueue(response);
        
        public IEnumerable<HttpRequestMessage> GetRequests() =>
            _requests;

        public HttpRequestMessage AwaitRequest()
        {
            Assert.True(_requests.TryTake(out var req, TimeSpan.FromSeconds(5)), "timed out waiting for request");
            return req;
        }
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

        public static StubResponse StartStream(Encoding encoding, params StreamAction[] actions)
        {
            return new StubResponseWithStream(actions, encoding);
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
        private readonly Encoding _encoding;
        private readonly bool _specifiedEncoding;

        public StubResponseWithStream(StreamAction[] actions, Encoding encoding = null)
        {
            _actions = actions;
            _encoding = encoding ?? Encoding.UTF8;
            _specifiedEncoding = encoding != null;
        }

        override public HttpResponseMessage MakeResponse(CancellationToken cancellationToken)
        {
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
            var pipe = new Pipe();
            var readStream = pipe.Reader.AsStream(true);
            var content = new StreamContent(readStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            if (_specifiedEncoding)
            {
                content.Headers.ContentType.CharSet = _encoding.HeaderName;
            }
            httpResponse.Content = content;

            Task.Run(() => WriteStreamingResponse(pipe.Writer, readStream, cancellationToken));

            return httpResponse;
        }
        
        private async Task WriteStreamingResponse(PipeWriter output, Stream readStream, CancellationToken cancellationToken)
        {
            try
            {
                foreach (var action in _actions)
                {
                    if (action.CloseEarly)
                    {
                        readStream.Close();
                        output.Complete();
                        return;
                    }
                    if (action.Delay != TimeSpan.Zero)
                    {
                        await Task.Delay(action.Delay, cancellationToken);
                    }
                    if (action.ShouldQuit())
                    {
                        output.Complete();
                        return;
                    }
                    var data = _encoding.GetBytes(action.Content);
                    await output.WriteAsync(new ReadOnlyMemory<byte>(data), cancellationToken);
                }
                // if we've run out of actions, leave the stream open until it's cancelled
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
            }
            catch (Exception)
            {
                // just exit
            }
        }
    }
    
    public class StreamAction
    {
        internal TimeSpan Delay { get; private set; }
        internal string Content { get; private set; }
        internal bool CloseEarly { get; private set; }

        public bool ShouldQuit() => Content is null;

        public static StreamAction Write(string content) =>
            new StreamAction { Content = content };

        public static StreamAction Write(MessageEvent e)
        {
            var s = new StringBuilder();
            if (e.Name != null)
            {
                s.Append("event:").Append(e.Name).Append("\n");
            }
            foreach (var line in e.Data.Split('\n'))
            {
                s.Append("data:").Append(line).Append("\n");
            }
            if (e.LastEventId != null)
            {
                s.Append("id:").Append(e.LastEventId).Append("\n");
            }
            return Write(s.ToString() + "\n");
        }

        public static StreamAction CloseStream() => new StreamAction();

        public StreamAction AfterDelay(TimeSpan delay) =>
            new StreamAction { Content = this.Content, CloseEarly = this.CloseEarly, Delay = delay };
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
