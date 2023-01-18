using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using LaunchDarkly.TestHelpers.HttpTest;

namespace TestService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var quitSignal = new EventWaitHandle(false, EventResetMode.AutoReset);
            var app = new Webapp(quitSignal);
            var server = HttpServer.Start(8000, app.Handler);
            server.Recorder.Enabled = false;
            quitSignal.WaitOne();
            server.Dispose();
        }
    }

    public class Webapp
    {
        public readonly Handler Handler;

        private readonly ILogAdapter _logging = Logs.ToConsole;
        private readonly Logger _baseLogger;
        private readonly ConcurrentDictionary<string, StreamEntity> _streams =
            new ConcurrentDictionary<string, StreamEntity>();
        private readonly EventWaitHandle _quitSignal;
        private volatile int _lastStreamId = 0;

        public Webapp(EventWaitHandle quitSignal)
        {
            _quitSignal = quitSignal;
            _baseLogger = _logging.Logger("service");

            var service = new SimpleJsonService();
            Handler = service.Handler;

            service.Route(HttpMethod.Get, "/", GetStatus);
            service.Route(HttpMethod.Delete, "/", ForceQuit);
            service.Route<StreamOptions>(HttpMethod.Post, "/", PostCreateClient);
            service.Route<CommandParams, object>(HttpMethod.Post, "/streams/(.*)", PostStreamCommand);
            service.Route(HttpMethod.Delete, "/streams/(.*)", DeleteClient);
        }

        SimpleResponse<Status> GetStatus(IRequestContext context) =>
            SimpleResponse.Of(200, new Status
            {
                Capabilities = new string[]
                {
                    "comments",
                    "headers",
                    "last-event-id",
                    "post",
                    "read-timeout",
                    "report",
                    "restart"
                }
            });

        SimpleResponse ForceQuit(IRequestContext context)
        {
            _logging.Logger("").Info("Test harness has told us to exit");

            // The web server won't send the response till we return, so we'll defer the actual shutdown
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                _quitSignal.Set();
            });

            return SimpleResponse.Of(204);
        }

        SimpleResponse PostCreateClient(IRequestContext context, StreamOptions options)
        {
            var testLogger = _logging.Logger(options.Tag);
            testLogger.Info("Starting SSE client");

            var id = Interlocked.Increment(ref _lastStreamId);
            var streamId = id.ToString();
            var stream = new StreamEntity(options, testLogger);
            _streams[streamId] = stream;

            var resourceUrl = "/streams/" + streamId;
            return SimpleResponse.Of(201).WithHeader("Location", resourceUrl);
        }

        SimpleResponse<object> PostStreamCommand(IRequestContext context, CommandParams command)
        {
            var id = context.GetPathParam(0);
            if (!_streams.TryGetValue(id, out var stream))
            {
                return SimpleResponse.Of<object>(404, null);
            }

            if (stream.DoCommand(command.Command))
            {
                return SimpleResponse.Of<object>(202, null);
            }
            else
            {
                return SimpleResponse.Of<object>(400, null);
            }
        }

        SimpleResponse DeleteClient(IRequestContext context)
        {
            var id = context.GetPathParam(0);
            if (!_streams.TryGetValue(id, out var stream))
            {
                _baseLogger.Error("Got delete request for unknown stream ID: {0}", id);
                return SimpleResponse.Of(404);
            }
            stream.Close();
            _streams.TryRemove(id, out _);

            return SimpleResponse.Of(204);
        }
    }
}
