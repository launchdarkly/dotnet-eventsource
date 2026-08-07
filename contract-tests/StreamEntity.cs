using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using LaunchDarkly.EventSource;
using LaunchDarkly.Logging;

namespace TestService
{
    /// <summary>
    /// Wraps a single LaunchDarkly.EventSource.EventSource instance driven by the SSE contract-tests
    /// harness. Subscribes to the SSE client's events and forwards each one to the harness callback
    /// URL as a JSON message.
    /// </summary>
    public class StreamEntity
    {
        private readonly StreamOptions _options;
        private readonly Logger _logger;
        private readonly HttpClient _callbackClient;
        private readonly EventSource _eventSource;
        private int _callbackMessageCounter;
        private volatile bool _closed;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        public StreamEntity(StreamOptions options, ILogAdapter logAdapter)
        {
            _options = options;
            _logger = logAdapter.Logger(options.Tag ?? "stream");
            _callbackClient = new HttpClient();

            _logger.Info("Opening stream to {0}", options.StreamUrl);

            var configBuilder = Configuration.Builder(new Uri(options.StreamUrl));

            if (options.Headers != null)
            {
                foreach (var kv in options.Headers)
                {
                    // Content-Type is a content header, not a request header; .NET's HttpHeaders
                    // throws if we try to add it as a request header. We consume this header
                    // separately below when constructing the request body.
                    if (string.Equals(kv.Key, "content-type", System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    configBuilder.RequestHeader(kv.Key, kv.Value);
                }
            }
            if (options.InitialDelayMs.HasValue)
            {
                configBuilder.InitialRetryDelay(TimeSpan.FromMilliseconds(options.InitialDelayMs.Value));
            }
            if (options.ReadTimeoutMs.HasValue)
            {
                configBuilder.ReadTimeout(TimeSpan.FromMilliseconds(options.ReadTimeoutMs.Value));
            }
            if (!string.IsNullOrEmpty(options.LastEventId))
            {
                configBuilder.LastEventId(options.LastEventId);
            }
            if (!string.IsNullOrEmpty(options.Method))
            {
                configBuilder.Method(new HttpMethod(options.Method));
                if (!string.IsNullOrEmpty(options.Body))
                {
                    // Match the same case-insensitive comparison used above when skipping
                    // Content-Type from the request-header loop, so both operations agree on
                    // what counts as the Content-Type key.
                    string contentType = "text/plain; charset=utf-8";
                    if (options.Headers != null)
                    {
                        foreach (var kv in options.Headers)
                        {
                            if (string.Equals(kv.Key, "content-type", System.StringComparison.OrdinalIgnoreCase))
                            {
                                contentType = kv.Value;
                                break;
                            }
                        }
                    }
                    var bodyString = options.Body;
                    var mediaType = contentType.Split(';')[0].Trim();
                    var charset = Encoding.UTF8;
                    configBuilder.RequestBodyFactory(() =>
                        new StringContent(bodyString, charset, mediaType));
                }
            }

            _eventSource = new EventSource(configBuilder.Build());
            _eventSource.MessageReceived += OnMessageReceived;
            _eventSource.Error += OnError;
            // Deliberately not subscribing to CommentReceived: the "comments" capability is
            // not declared because .NET EventSource returns comment strings with the leading
            // colon still attached, which does not match the harness's expected shape. If we
            // forwarded comments anyway, the harness would receive unexpected comment messages
            // in tests that assume no comment reporting.

            // Fire-and-forget: EventSource fires events on its own internal thread as data arrives.
            _ = _eventSource.StartAsync();
        }

        public bool DoCommand(string command)
        {
            _logger.Info("Test harness sent command: {0}", command);
            if (command == "restart")
            {
                _eventSource.Restart(false);
                return true;
            }
            return false;
        }

        public void Close()
        {
            _closed = true;
            _eventSource.MessageReceived -= OnMessageReceived;
            _eventSource.Error -= OnError;
            _eventSource.Close();
            _callbackClient.Dispose();
            _logger.Info("Test ended");
        }

        private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            _logger.Info("Received event from stream ({0})", e.EventName);
            var msg = new Message
            {
                Kind = "event",
                Event = new EventMessage
                {
                    Type = e.EventName,
                    Data = e.Message.Data,
                    Id = e.Message.LastEventId,
                },
            };
            SendCallback(msg);
        }

        private void OnError(object sender, ExceptionEventArgs e)
        {
            _logger.Info("Received error from stream: {0}", e.Exception);
            SendCallback(new Message { Kind = "error", Error = e.Exception.ToString() });
        }

        private void SendCallback(Message message)
        {
            if (_closed)
            {
                return;
            }
            var counter = Interlocked.Increment(ref _callbackMessageCounter);
            var url = _options.CallbackUrl + "/" + counter;
            var json = JsonSerializer.Serialize(message, JsonOptions);
            try
            {
                using var content = new StringContent(json, Encoding.UTF8);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                var resp = _callbackClient.PostAsync(url, content).GetAwaiter().GetResult();
                if ((int)resp.StatusCode >= 300)
                {
                    _logger.Error("Callback to {0} returned HTTP {1}", url, (int)resp.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Callback to {0} failed: {1}", url, ex.GetType().Name);
            }
        }
    }
}
