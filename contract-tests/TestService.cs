using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using LaunchDarkly.TestHelpers.HttpTest;

namespace TestService
{
    /// <summary>
    /// HTTP application logic for the SSE contract-tests service. Implements the endpoints
    /// described in launchdarkly/sse-contract-tests/docs/service_spec.md for the
    /// LaunchDarkly.EventSource library. This is the .NET analogue of ssetest.TestService in
    /// okhttp-eventsource's contract-tests service.
    ///
    /// The entry point that starts the HTTP server lives in Program.cs (desktop) and in
    /// ../contract-tests-android/MainActivity.cs (Android). Both instantiate this Webapp and
    /// hand its Handler to HttpServer.Start; the routing logic here is platform-agnostic.
    /// </summary>
    public class Webapp
    {
        // Capabilities the LaunchDarkly.EventSource library implements correctly against the
        // sse-contract-tests harness. Each entry here was verified end-to-end by running the
        // harness's tests for that capability. Deliberately omitted:
        //
        //   * "comments" -- CommentReceived fires with the raw line including the leading colon;
        //     the harness expects the colon stripped. See EventParser.cs where colonPos==0 stores
        //     `line` (with colon) as ValueString. Real .NET SDK behavior; not fixed here.
        //   * "event-type-listeners" -- not applicable; MessageReceived fires for all event types
        //     without explicit registration.
        //   * "server-directed-shutdown-request" -- .NET retries on 204 instead of halting.
        //     EventSourceService.ValidateResponse throws on 204, EventSource.cs top-level loop
        //     re-enters on Closed (not Shutdown) state. Real .NET SDK behavior; not fixed here.
        private static readonly string[] Capabilities = new[]
        {
            "bom",
            "headers",
            "last-event-id",
            "payload-size-stress-testable",
            "post",
            "read-timeout",
            "report",
            "restart",
        };

        public readonly Handler Handler;

        private readonly ILogAdapter _logging = Logs.ToConsole;
        private readonly ConcurrentDictionary<string, StreamEntity> _streams =
            new ConcurrentDictionary<string, StreamEntity>();
        private readonly EventWaitHandle _quitSignal;
        private volatile int _lastStreamId = 0;

        public Webapp(EventWaitHandle quitSignal)
        {
            _quitSignal = quitSignal;

            var service = new SimpleJsonService();
            Handler = service.Handler;

            service.Route(HttpMethod.Get, "/", GetStatus);
            service.Route(HttpMethod.Delete, "/", ForceQuit);
            service.Route<StreamOptions>(HttpMethod.Post, "/", PostCreateStream);
            service.Route<CommandParams>(HttpMethod.Post, "/streams/(.*)", PostStreamCommand);
            service.Route(HttpMethod.Delete, "/streams/(.*)", DeleteStream);
        }

        SimpleResponse<Status> GetStatus(IRequestContext context) =>
            SimpleResponse.Of(200, new Status { Capabilities = Capabilities });

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

        SimpleResponse PostCreateStream(IRequestContext context, StreamOptions opts)
        {
            var stream = new StreamEntity(opts, _logging);

            var id = Interlocked.Increment(ref _lastStreamId);
            var streamId = id.ToString();
            _streams[streamId] = stream;

            var resourceUrl = "/streams/" + streamId;
            return SimpleResponse.Of(201).WithHeader("Location", resourceUrl);
        }

        SimpleResponse PostStreamCommand(IRequestContext context, CommandParams cmd)
        {
            var id = context.GetPathParam(0);
            if (!_streams.TryGetValue(id, out var stream))
            {
                return SimpleResponse.Of(404);
            }
            if (!stream.DoCommand(cmd.Command))
            {
                return SimpleResponse.Of(400);
            }
            return SimpleResponse.Of(204);
        }

        SimpleResponse DeleteStream(IRequestContext context)
        {
            var id = context.GetPathParam(0);
            if (!_streams.TryGetValue(id, out var stream))
            {
                return SimpleResponse.Of(404);
            }
            stream.Close();
            _streams.TryRemove(id, out _);
            return SimpleResponse.Of(204);
        }
    }
}
