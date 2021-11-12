using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LaunchDarkly.EventSource;
using LaunchDarkly.Logging;

namespace TestService
{
    public class StreamEntity
    {
        private static HttpClient _httpClient = new HttpClient();

        private readonly EventSource _stream;
        private readonly StreamOptions _options;
        private readonly Logger _log;
        private volatile bool _closed;

        public StreamEntity(
            StreamOptions options,
            Logger log
        )
        {
            _options = options;
            _log = log;

            var builder = Configuration.Builder(new Uri(options.StreamUrl));
            builder.Logger(log);
            if (_options.Headers != null)
            {
                foreach (var kv in _options.Headers)
                {
                    if (kv.Key.ToLower() != "content-type")
                    {
                        builder.RequestHeader(kv.Key, kv.Value);
                    }
                }
            }
            if (_options.InitialDelayMs != null)
            {
                builder.InitialRetryDelay(TimeSpan.FromMilliseconds(_options.InitialDelayMs.Value));
            }
            if (_options.LastEventId != null)
            {
                builder.LastEventId(_options.LastEventId);
            }
            if (_options.Method != null)
            {
                builder.Method(new System.Net.Http.HttpMethod(_options.Method));
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
                builder.RequestBody(_options.Body, contentType);
            }
            if (_options.ReadTimeoutMs != null)
            {
                builder.ReadTimeout(TimeSpan.FromMilliseconds(_options.ReadTimeoutMs.Value));
            }

            _log.Info("Opening stream from {0}", _options.StreamUrl);

            _stream = new EventSource(builder.Build());
            _stream.MessageReceived += (sender, args) =>
            {
                _log.Info("Received event from stream ({0})", args.EventName);
                Task.Run(() => SendMessage(new Message
                {
                    Kind = "event",
                    Event = new EventMessage
                    {
                        Type = args.EventName,
                        Data = args.Message.Data,
                        Id = args.Message.LastEventId
                    }
                }));
            };
            _stream.CommentReceived += (sender, args) =>
            {
                var comment = args.Comment;
                if (comment.StartsWith(":"))
                {
                    comment = comment.Substring(1); // this SSE client includes the colon in the comment
                }
                _log.Info("Received comment from stream: {0}", comment);
                Task.Run(() => SendMessage(new Message
                {
                    Kind = "comment",
                    Comment = comment
                }));
            };
            _stream.Error += (sender, args) =>
            {
                var exDesc = LogValues.ExceptionSummary(args.Exception);
                _log.Info("Received error from stream: {0}", exDesc);
                Task.Run(() => SendMessage(new Message
                {
                    Kind = "error",
                    Error = exDesc.ToString()
                }));
            };

            Task.Run(() => _stream.StartAsync());
        }
        
        public void Close()
        {
            _closed = true;
            _stream.Close();
            _log.Info("Test ended");
        }

        public bool DoCommand(string command)
        {
            if (command == "restart")
            {
                _stream.Restart(false);
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
            using (var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_options.CallbackUrl)))
            using (var stringContent = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                request.Content = stringContent;
                try
                {
                    using (var response = await _httpClient.SendAsync(request))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            _log.Error("Callback to {0} returned HTTP {1}", _options.CallbackUrl, response.StatusCode);
                        }
                    }
                }
                catch (Exception e)
                {
                    _log.Error("Callback to {0} failed: {1}", _options.CallbackUrl, e.GetType());
                }
            }
        }
    }
}
