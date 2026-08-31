using System;
using System.Net;
using System.Threading.Tasks;
using LaunchDarkly.TestHelpers.HttpTest;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.EventSource.Tests.TestHelpers;

namespace LaunchDarkly.EventSource.Tests
{
    public class TemporaryRetryDelayBoundsTest : BaseTest
    {
        private static readonly TimeSpan ConfiguredInitial = TimeSpan.FromMilliseconds(20);
        private static readonly TimeSpan ConfiguredMax = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan TempInitial = TimeSpan.FromMilliseconds(60);
        private static readonly TimeSpan TempMax = TimeSpan.FromMilliseconds(500);

        // Long enough that no test connection can reach it, so temporary bounds are never
        // auto-cleared in tests that don't want them to be.
        private static readonly TimeSpan NeverResets = TimeSpan.FromSeconds(30);

        public TemporaryRetryDelayBoundsTest(ITestOutputHelper testOutput) : base(testOutput) { }

        private EventSource MakeConfigured(Action<ConfigurationBuilder> extra = null) =>
            MakeEventSource(_uri, c =>
            {
                c.InitialRetryDelay(ConfiguredInitial).MaxRetryDelay(ConfiguredMax);
                extra?.Invoke(c);
            });

        // ---- bounds installation and reversion ----

        [Fact]
        public void FreshEventSourceUsesConfiguredBounds()
        {
            using (var es = MakeConfigured())
            {
                Assert.Equal(ConfiguredInitial, es.BackOff.GetMinimumDelay());
                Assert.Equal(ConfiguredMax, es.BackOff.GetMaximumDelay());
            }
        }

        [Fact]
        public void SetTemporaryBoundsChangesTheActiveBounds()
        {
            using (var es = MakeConfigured())
            {
                es.SetTemporaryRetryDelayBounds(TempInitial, TempMax);

                Assert.Equal(TempInitial, es.BackOff.GetMinimumDelay());
                Assert.Equal(TempMax, es.BackOff.GetMaximumDelay());
            }
        }

        [Fact]
        public void ClearTemporaryBoundsRestoresConfiguredBounds()
        {
            using (var es = MakeConfigured())
            {
                es.SetTemporaryRetryDelayBounds(TempInitial, TempMax);
                es.ClearTemporaryRetryDelayBounds();

                Assert.Equal(ConfiguredInitial, es.BackOff.GetMinimumDelay());
                Assert.Equal(ConfiguredMax, es.BackOff.GetMaximumDelay());
            }
        }

        [Fact]
        public void ClearTemporaryBoundsWithNoneSetIsNoOp()
        {
            using (var es = MakeConfigured())
            {
                es.BackOff.GetNextBackOff();
                es.BackOff.GetNextBackOff();

                es.ClearTemporaryRetryDelayBounds();

                // The configured bounds were already in effect, so nothing changed -- in particular
                // the backoff level was not reset.
                Assert.Equal(2, es.BackOff.GetBackoffN());
                Assert.Equal(ConfiguredInitial, es.BackOff.GetMinimumDelay());
            }
        }

        // ---- backoff level vs. reconnect attempt count ----

        [Fact]
        public void SetTemporaryBoundsResetsBackoffN()
        {
            using (var es = MakeConfigured())
            {
                es.BackOff.GetNextBackOff();
                es.BackOff.GetNextBackOff();
                es.BackOff.GetNextBackOff();

                es.SetTemporaryRetryDelayBounds(TempInitial, TempMax);

                Assert.Equal(0, es.BackOff.GetBackoffN());
            }
        }

        [Fact]
        public void SettingTheSameTemporaryBoundsAgainDoesNotResetTheBackoffN()
        {
            using (var es = MakeConfigured())
            {
                es.SetTemporaryRetryDelayBounds(TempInitial, TempMax);
                es.BackOff.GetNextBackOff();
                es.BackOff.GetNextBackOff();

                es.SetTemporaryRetryDelayBounds(TempInitial, TempMax);

                Assert.Equal(2, es.BackOff.GetBackoffN());
            }
        }

        [Fact]
        public void RepeatedlySettingTemporaryBoundsDoesNotPinTheDelay()
        {
            using (var es = MakeConfigured())
            {
                // A consumer that reapplies the same bounds on every error must still back off.
                for (int i = 0; i < 5; i++)
                {
                    es.SetTemporaryRetryDelayBounds(TempInitial, TempMax);
                    es.BackOff.GetNextBackOff();
                }

                Assert.Equal(5, es.BackOff.GetBackoffN());
            }
        }

        [Fact]
        public void ReEnteringTemporaryBoundsAfterClearResetsTheBackoffN()
        {
            using (var es = MakeConfigured())
            {
                es.SetTemporaryRetryDelayBounds(TempInitial, TempMax);
                es.BackOff.GetNextBackOff();
                es.BackOff.GetNextBackOff();
                es.BackOff.GetNextBackOff();
                Assert.Equal(3, es.BackOff.GetBackoffN());

                es.ClearTemporaryRetryDelayBounds();
                es.SetTemporaryRetryDelayBounds(TempInitial, TempMax);

                // The progression restarts rather than resuming where it left off, so a connection
                // that recovered and then failed again is probed sooner.
                Assert.Equal(0, es.BackOff.GetBackoffN());
            }
        }

        // ---- argument handling ----

        [Fact]
        public void TemporaryBoundsWithInitialAboveMaxAreLimitedToMax()
        {
            using (var es = MakeConfigured())
            {
                es.SetTemporaryRetryDelayBounds(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));

                Assert.Equal(1000L, es.BackOff.GetUnjitteredMillisecondsForN(0));
                Assert.Equal(1000L, es.BackOff.GetUnjitteredMillisecondsForN(4));
            }
        }

        [Fact]
        public void NegativeTemporaryBoundsClampToZeroWithoutThrowing()
        {
            using (var es = MakeConfigured())
            {
                es.SetTemporaryRetryDelayBounds(TimeSpan.FromMilliseconds(-10),
                    TimeSpan.FromMilliseconds(-20));

                Assert.Equal(TimeSpan.Zero, es.BackOff.GetMinimumDelay());
                Assert.Equal(TimeSpan.Zero, es.BackOff.GetMaximumDelay());
            }
        }

        [Fact]
        public void AbsurdTemporaryBoundsCannotBreakTheReconnectLoop()
        {
            using (var es = MakeConfigured())
            {
                es.SetTemporaryRetryDelayBounds(TimeSpan.FromMilliseconds(1000), TimeSpan.MaxValue);

                // No caller-supplied bound may make a later delay computation throw; that would
                // escape StartAsync and stop the stream for good.
                for (int i = 0; i < 64; i++)
                {
                    es.BackOff.GetNextBackOff();
                }
            }
        }

        [Fact]
        public async Task SetTemporaryBoundsIsSafeFromAnotherThread()
        {
            using (var es = MakeConfigured())
            {
                const int iterations = 500;
                var writer = Task.Run(() =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        es.SetTemporaryRetryDelayBounds(
                            TimeSpan.FromMilliseconds(10 + (i % 2)),
                            TimeSpan.FromMilliseconds(100 + (i % 2)));
                    }
                });

                int computed = 0;
                for (int i = 0; i < iterations; i++)
                {
                    es.BackOff.GetNextBackOff();
                    computed++;
                }
                // Bounded so that a genuine deadlock fails the test rather than hanging it.
                var finished = await Task.WhenAny(writer, Task.Delay(TimeSpan.FromSeconds(10)));
                Assert.Same(writer, finished);
                await writer; // surfaces any exception thrown on the writer thread

                Assert.Equal(iterations, computed);
                // Bounds must end up as one of the two pairs the writer set, never a torn mix.
                Assert.Contains(es.BackOff.GetMinimumDelay().TotalMilliseconds, new double[] { 10, 11 });
                Assert.Contains(es.BackOff.GetMaximumDelay().TotalMilliseconds, new double[] { 100, 101 });
            }
        }

        // ---- wiring through a live stream ----

        [Fact]
        public void BoundsSetFromErrorHandlerTakeEffect()
        {
            var handler = Handlers.Sequential(
                Handlers.Status((int)HttpStatusCode.Unauthorized),
                StartStream().Then(LeaveStreamOpen())
                );

            WithServerAndEventSource(handler,
                c => c.InitialRetryDelay(ConfiguredInitial).MaxRetryDelay(ConfiguredMax)
                      .BackoffResetThreshold(NeverResets),
                (server, es) =>
                {
                    es.Error += (_, e) => es.SetTemporaryRetryDelayBounds(TempInitial, TempMax);

                    _ = Task.Run(es.StartAsync);

                    server.Recorder.RequireRequest();
                    server.Recorder.RequireRequest(); // the reconnect, which used the new bounds

                    Assert.Equal(TempInitial, es.BackOff.GetMinimumDelay());
                    Assert.Equal(TempMax, es.BackOff.GetMaximumDelay());
                });
        }

        [Fact]
        public void SustainedHealthyConnectionClearsTemporaryBounds()
        {
            var handler = Handlers.Sequential(
                StartStream().Then(WriteEvent(new MessageEvent("put", "hello", _uri))),
                StartStream().Then(LeaveStreamOpen())
                );

            WithServerAndEventSource(handler,
                // A zero threshold means any connection that opened counts as healthy, which makes
                // the reset deterministic rather than timing-dependent.
                c => c.InitialRetryDelay(ConfiguredInitial).MaxRetryDelay(ConfiguredMax)
                      .BackoffResetThreshold(TimeSpan.Zero),
                (server, es) =>
                {
                    es.SetTemporaryRetryDelayBounds(TempInitial, TempMax);

                    _ = Task.Run(es.StartAsync);

                    server.Recorder.RequireRequest();
                    server.Recorder.RequireRequest();

                    Assert.Equal(ConfiguredInitial, es.BackOff.GetMinimumDelay());
                    Assert.Equal(ConfiguredMax, es.BackOff.GetMaximumDelay());
                });
        }

        [Fact]
        public void ConnectionShorterThanThresholdDoesNotClearTemporaryBounds()
        {
            var handler = Handlers.Sequential(
                StartStream().Then(WriteEvent(new MessageEvent("put", "hello", _uri))),
                StartStream().Then(LeaveStreamOpen())
                );

            WithServerAndEventSource(handler,
                c => c.InitialRetryDelay(ConfiguredInitial).MaxRetryDelay(ConfiguredMax)
                      .BackoffResetThreshold(NeverResets),
                (server, es) =>
                {
                    es.SetTemporaryRetryDelayBounds(TempInitial, TempMax);

                    _ = Task.Run(es.StartAsync);

                    server.Recorder.RequireRequest();
                    server.Recorder.RequireRequest();

                    Assert.Equal(TempInitial, es.BackOff.GetMinimumDelay());
                    Assert.Equal(TempMax, es.BackOff.GetMaximumDelay());
                });
        }

        [Fact]
        public void RestartDoesNotRevertTemporaryBounds()
        {
            // The chunk write is what flushes the response headers, so that Opened fires.
            var handler = StartStream().Then(Handlers.WriteChunkString(":hi\n"))
                .Then(LeaveStreamOpen());

            WithServerAndEventSource(handler,
                c => c.InitialRetryDelay(ConfiguredInitial).MaxRetryDelay(ConfiguredMax)
                      .BackoffResetThreshold(NeverResets),
                (server, es) =>
                {
                    var sink = new EventSink(es, _testLogging);

                    _ = Task.Run(es.StartAsync);
                    sink.ExpectActions(EventSink.OpenedAction());

                    es.SetTemporaryRetryDelayBounds(TempInitial, TempMax);
                    es.Restart(true);

                    server.Recorder.RequireRequest();

                    // Restart resets the backoff level, but within whatever bounds are active --
                    // it does not discard temporary bounds.
                    Assert.Equal(TempInitial, es.BackOff.GetMinimumDelay());
                    Assert.Equal(TempMax, es.BackOff.GetMaximumDelay());
                });
        }
    }
}
