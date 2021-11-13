using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace TestService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseUrls("http://0.0.0.0:8000")
                .UseStartup<Webapp>()
                .Build();

            host.Run();
        }
    }

    public class Webapp
    {
        private readonly ILogAdapter _logging = Logs.ToConsole;
        private readonly ConcurrentDictionary<string, StreamEntity> _streams =
            new ConcurrentDictionary<string, StreamEntity>();
        private volatile int _lastStreamId = 0;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRouting();
        }

        public void Configure(IApplicationBuilder app)
        {
            var routeBuilder = new RouteBuilder(app);

            routeBuilder.MapGet("", GetStatus);
            routeBuilder.MapDelete("", ForceQuit);
            routeBuilder.MapPost("", PostCreateStream);
            routeBuilder.MapPost("/streams/{id}", PostStreamCommand);
            routeBuilder.MapDelete("/streams/{id}", DeleteStream);

            var routes = routeBuilder.Build();
            app.UseRouter(routes);
        }

        T ReadJson<T>(HttpRequest req)
        {
            using (var bodyStream = new StreamReader(req.Body))
            {
                return JsonSerializer.Deserialize<T>(bodyStream.ReadToEnd());
            }
        }

        async Task WriteJson<T>(HttpResponse resp, T value)
        {
            resp.ContentType = "application/json";
            await resp.WriteAsync(JsonSerializer.Serialize(value));
        }

        async Task GetStatus(HttpContext context)
        {
            var status = new Status
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
            };
            await WriteJson(context.Response, status);
        }

        Task ForceQuit(HttpContext context)
        {
            _logging.Logger("").Info("Test harness has told us to exit");
            context.Response.StatusCode = StatusCodes.Status204NoContent;

            System.Environment.Exit(0);

            return Task.CompletedTask; // never reached
        }

        Task PostCreateStream(HttpContext context)
        {
            var options = ReadJson<StreamOptions>(context.Request);

            var id = Interlocked.Increment(ref _lastStreamId);
            var streamId = id.ToString();
            var stream = new StreamEntity(options, _logging.Logger(options.Tag));
            _streams[streamId] = stream;

            var resourceUrl = "/streams/" + streamId;
            context.Response.Headers["Location"] = resourceUrl;
            context.Response.StatusCode = StatusCodes.Status201Created;

            return Task.CompletedTask;
        }

        Task PostStreamCommand(HttpContext context)
        {
            var id = context.GetRouteValue("id").ToString();
            if (!_streams.TryGetValue(id, out var stream))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }

            var command = ReadJson<CommandParams>(context.Request);
            if (stream.DoCommand(command.Command))
            {
                context.Response.StatusCode = StatusCodes.Status202Accepted;
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }

            return Task.CompletedTask;
        }

        Task DeleteStream(HttpContext context)
        {
            var id = context.GetRouteValue("id").ToString();
            if (!_streams.TryGetValue(id, out var stream))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }
            stream.Close();
            _streams.TryRemove(id, out _);

            context.Response.StatusCode = StatusCodes.Status204NoContent;

            return Task.CompletedTask;
        }
    }
}
