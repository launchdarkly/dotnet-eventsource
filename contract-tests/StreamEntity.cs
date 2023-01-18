using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using LaunchDarkly.EventSource;
using LaunchDarkly.EventSource.Events;
using LaunchDarkly.EventSource.Exceptions;
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

            var builder = Configuration.Builder(httpConfig)
                .ErrorStrategy(ErrorStrategy.AlwaysContinue) // see comments in RunAsync
                .Logger(log);

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

            Task.Run(RunAsync);
        }

        private async Task RunAsync()
        {
            // A typical SSE-based application would only be interested in the events that
            // are represented by EventMessage, so it could more simply call ReadMessageAsync()
            // and receive only that type. But for the contract tests, we want to know more
            // details such as comments and error conditions.

            while (_stream.ReadyState != ReadyState.Shutdown)
            {
                try
                {
                    var e = await _stream.ReadAnyEventAsync();

                    if (e is MessageEvent me)
                    {
                        _log.Info("Received event from stream (type: {0}, data: {1})",
                            me.Name, me.Data);
                        await SendMessage(new CallbackMessage
                        {
                            Kind = "event",
                            Event = new CallbackEventMessage
                            {
                                Type = me.Name,
                                Data = me.Data,
                                Id = me.LastEventId
                            }
                        });
                    }
                    else if (e is CommentEvent ce)
                    {
                        _log.Info("Received comment from stream: {0}", ce.Text);
                        await SendMessage(new CallbackMessage
                        {
                            Kind = "comment",
                            Comment = ce.Text
                        });
                    }
                    else if (e is FaultEvent fe)
                    {
                        if (fe.Exception is StreamClosedByCallerException)
                        {
                            // This one is special because it simply means we deliberately
                            // closed the stream ourselves, so we don't need to report it
                            // to the test harness.
                            continue;
                        }
                        var exDesc = LogValues.ExceptionSummary(fe.Exception).ToString();
                        _log.Info("Received error from stream: {0}", exDesc);
                        await SendMessage(new CallbackMessage
                        {
                            Kind = "error",
                            Error = exDesc
                        });
                    }
                }
                catch (Exception ex)
                {
                    // Because we specified ErrorStrategy.AlwaysContinue, EventSource
                    // will normally report any errors on the stream as just part of the
                    // stream, so we will get them as FaultEvents and then it will
                    // transparently reconnect. Any exception that is thrown here
                    // probably means there is a bug, so we'll report it to the test
                    // harness (likely causing test to fail) and also log a detailed
                    // stacktrace.
                    _log.Error("Unexpected exception: {0} {1}", LogValues.ExceptionSummary(ex),
                        LogValues.ExceptionTrace(ex));
                    await SendMessage(new CallbackMessage
                    {
                        Kind = "error",
                        Error = LogValues.ExceptionSummary(ex).ToString()
                    });
                }
            }
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
                        else
                        {
                            _log.Info("Callback to {0} succeeded", uri);
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
