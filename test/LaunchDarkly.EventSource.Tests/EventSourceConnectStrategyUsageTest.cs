using System;
using System.IO;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using Xunit;

namespace LaunchDarkly.EventSource
{
    public class EventSourceConnectStrategyUsageTest
    {
        public class ConnectStrategyFromLambda : ConnectStrategy
        {
            private readonly Func<Logger, Client> _fn;

            public override Uri Origin => new Uri("http://test/origin");

            public ConnectStrategyFromLambda(Func<Logger, Client> fn) { _fn = fn; }

            public override Client CreateClient(Logger logger) =>
                _fn(logger);
        }

        public class ClientFromLambda : ConnectStrategy.Client
        {
            private readonly Func<Params, Task<Result>> _fn;
            public volatile bool Closed = false;

            public ClientFromLambda(Func<Params, Task<Result>> fn) { _fn = fn; }

            public override Task<Result> ConnectAsync(Params parameters) =>
                _fn(parameters);

            public override void Dispose() { Closed = true; }
        }

        public class Disposable : IDisposable
        {
            public volatile bool Closed = false;

            public void Dispose()
            {
                if (Closed)
                {
                    throw new InvalidOperationException("should not have been closed twice");
                }
                Closed = true;
            }
        }

        private static Stream MakeEmptyStream() =>
            new MemoryStream(new byte[0]);

        [Fact]
        public void ConnectStrategyIsCalledImmediatelyToCreateClient()
        {
            ClientFromLambda created = null;
            Logger receivedLogger = null;

            var strategy = new ConnectStrategyFromLambda(logger =>
            {
                var c = new ClientFromLambda(_ =>
                    throw new Exception("ConnectAsync should not be called"));
                created = c;
                receivedLogger = logger;
                return c;
            });

            var testLogger = Logger.WithAdapter(Logs.None, "");

            using (var es = new EventSource(
                new ConfigurationBuilder(strategy).Logger(testLogger).Build()))
            {
                Assert.NotNull(created);
                Assert.Same(receivedLogger, testLogger);
                Assert.False(created.Closed);
            }

            Assert.True(created.Closed);
        }

        [Fact]
        public async void ConnectIsCalledOnStart()
        {
            Stream createdStream = null;
            var strategy = new ConnectStrategyFromLambda(logger =>
                new ClientFromLambda(_ =>
                {
                    createdStream = MakeEmptyStream();
                    return Task.FromResult(new ConnectStrategy.Client.Result
                    {
                        Stream = createdStream
                    });
                }));

            using (var es = new EventSource(
                new ConfigurationBuilder(strategy).Build()))
            {
                Assert.Null(createdStream);
                await es.StartAsync();
                Assert.NotNull(createdStream);
            }
        }

        [Fact]
        public async void ConnectionCloserIsCalledOnClose()
        {
            Disposable closer = new Disposable();
            var strategy = new ConnectStrategyFromLambda(logger =>
                new ClientFromLambda(_ =>
                {
                    return Task.FromResult(new ConnectStrategy.Client.Result
                    {
                        Stream = MakeEmptyStream(),
                        Closer = closer
                    });
                }));

            using (var es = new EventSource(
                new ConfigurationBuilder(strategy).Build()))
            {
                await es.StartAsync();
                Assert.False(closer.Closed);
            }

            Assert.True(closer.Closed);
        }
    }
}

