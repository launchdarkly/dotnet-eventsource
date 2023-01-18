using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.EventSource;
using LaunchDarkly.EventSource.Background;
using LaunchDarkly.Logging;

namespace TestService
{
    public class StreamEntity
    {
        private static HttpClient _httpClient = new HttpClient();

        private readonly EventSource _stream;
        private readonly StreamOptions _options;
        private readonly Logger _log;
        private volatile int _callbackMessageCounter;
        private volatile bool _closed;

        public StreamEntity(
            StreamOptions options,
            Logger log
        )
        {
            _options = options;
            _log = log;

            var httpConfig = ConnectStrategy.Http(new Uri(options.StreamUrl));
            if (_options.Headers != null)
            {
                foreach (var kv in _options.Headers)
                {
                    if (kv.Key.ToLower() != "content-type")
                    {

                        httpConfig = httpConfig.Header(kv.Key, kv.Value);
                    }
                }
            }
            if (_options.Method != null)
            {
                httpConfig = httpConfig.Method(new HttpMethod(_options.Method));
                var contentType = "text/plain";
                if (_options.Headers != null)
                {
                    foreach (var kv in _options.Headers)
                    {
                        if (kv.Key.ToLower() == "content-type")
                        {
                            contentType = kv.Value;
                            if (contentType.Contains(";"))
                            {
                                contentType = contentType.Substring(0, contentType.IndexOf(";"));
                            }
                        }
                    }
                }
                httpConfig = httpConfig.RequestBody(_options.Body, contentType);
            }
            if (_options.ReadTimeoutMs != null)
            {
                httpConfig = httpConfig.ReadTimeout(TimeSpan.FromMilliseconds(_options.ReadTimeoutMs.Value));
            }

            var builder = Configuration.Builder(httpConfig);
            builder.Logger(log);
            if (_options.InitialDelayMs != null)
            {
                builder.InitialRetryDelay(TimeSpan.FromMilliseconds(_options.InitialDelayMs.Value));
            }
            if (_options.LastEventId != null)
            {
                builder.LastEventId(_options.LastEventId);
            }

            _log.Info("Opening stream from {0}", _options.StreamUrl);

            _stream = new EventSource(builder.Build());

            var backgroundEventSource = new BackgroundEventSource(_stream);
            backgroundEventSource.MessageReceived += async (sender, args) =>
            {
                _log.Info("Received event from stream (type: {0}, data: {1})",
                    args.EventName, args.Message.Data);
                await SendMessage(new Message
                {
                    Kind = "event",
                    Event = new EventMessage
                    {
                        Type = args.EventName,
                        Data = args.Message.Data,
                        Id = args.Message.LastEventId
                    }
                });
            };
            backgroundEventSource.CommentReceived += async (sender, args) =>
            {
                var comment = args.Comment;
                if (comment.StartsWith(":"))
                {
                    comment = comment.Substring(1); // this SSE client includes the colon in the comment
                }
                _log.Info("Received comment from stream: {0}", comment);
                await SendMessage(new Message
                {
                    Kind = "comment",
                    Comment = comment
                });
            };
            backgroundEventSource.Error += async (sender, args) =>
            {
                var exDesc = LogValues.ExceptionSummary(args.Exception);
                _log.Info("Received error from stream: {0}", exDesc);
                await SendMessage(new Message
                {
                    Kind = "error",
                    Error = exDesc.ToString()
                });
            };

            Task.Run(backgroundEventSource.RunAsync);
        }
        
        public void Close()
        {
            _closed = true;
            _log.Info("Closing");
            _stream.Close();
            _log.Info("Test ended");
        }

        public bool DoCommand(string command)
        {
            _log.Info("Test harness sent command: {0}", command);
            if (command == "restart")
            {
                _stream.Interrupt();
                return true;
            }
            return false;
        }

        private async Task SendMessage(object message)
        {
            if (_closed)
            {
                return;
            }
            var json = JsonSerializer.Serialize(message);
            _log.Info("Sending: {0}", json);
            var counter = Interlocked.Increment(ref _callbackMessageCounter);
            var uri = new Uri(_options.CallbackUrl + "/" + counter);

            using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
            using (var stringContent = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                request.Content = stringContent;
                try
                {
                    using (var response = await _httpClient.SendAsync(request))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            _log.Error("Callback to {0} returned HTTP {1}", uri, response.StatusCode);
                        }
                    }
                }
                catch (Exception e)
                {
                    _log.Error("Callback to {0} failed: {1}", uri, e.GetType());
                }
            }
        }
    }
}
