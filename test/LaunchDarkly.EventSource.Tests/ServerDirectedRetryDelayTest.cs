using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LaunchDarkly.TestHelpers.HttpTest;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.EventSource.Tests.TestHelpers;

namespace LaunchDarkly.EventSource.Tests
{
    /// <summary>
    /// Covers the SSE <c>retry:</c> field: a server-directed reconnection time that overrides the
    /// minimum delay used for subsequent backoffs.
    /// </summary>
    public class ServerDirectedRetryDelayTest : BaseTest
    {
        private static readonly TimeSpan ConfiguredInitial = TimeSpan.FromMilliseconds(20);
        private static readonly TimeSpan ConfiguredMax = TimeSpan.FromHours(24);
        private static readonly MessageEvent Marker = new MessageEvent("put", "hello", _uri);

        public ServerDirectedRetryDelayTest(ITestOutputHelper testOutput) : base(testOutput) { }

        // Emits a raw retry: line, then a normal event. Once the event arrives we know the retry
        // line has definitely been processed -- the same sequencing Java's tests use.
        private static Handler StreamWithRetryLine(string value) =>
            StartStream()
                .Then(Handlers.WriteChunkString("retry: " + value + "\n"))
                .Then(WriteEvent(Marker))
                .Then(LeaveStreamOpen());

        private void WithRetryLine(string value, Action<EventSource> assert,
            Action<ConfigurationBuilder> modConfig = null)
        {
            WithServerAndEventSource(StreamWithRetryLine(value),
                c =>
                {
                    c.InitialRetryDelay(ConfiguredInitial).MaxRetryDelay(ConfiguredMax)
                        .BackoffResetThreshold(TimeSpan.FromSeconds(30));
                    modConfig?.Invoke(c);
                },
                (server, es) =>
                {
                    var sink = new EventSink(es, _testLogging);
                    _ = Task.Run(es.StartAsync);

                    sink.ExpectActions(
                        EventSink.OpenedAction(),
                        EventSink.MessageReceivedAction(Marker));

                    assert(es);
                });
        }

        [Fact]
        public void RetryFieldSetsTheMinDelay()
        {
            WithRetryLine("300", es =>
            {
                Assert.Equal(TimeSpan.FromMilliseconds(300),
                    es.BackOff.GetServerDirectedMinDelay());
                Assert.Equal(300L, es.BackOff.GetUnjitteredMillisecondsForN(0));
            });
        }

        [Fact]
        public void RetryFieldOverridesTheConfiguredInitialDelay()
        {
            WithRetryLine("300", es =>
                Assert.NotEqual(ConfiguredInitial, es.BackOff.GetServerDirectedMinDelay()));
        }

        [Fact]
        public void RetryFieldOverridesTemporaryBounds()
        {
            WithRetryLine("300", es =>
            {
                es.SetTemporaryRetryDelayBounds(TimeSpan.FromMinutes(5), TimeSpan.FromHours(1));

                // The directed value outranks the temporary minimum, matching the reference
                // implementations, which apply a wire hint to every registered regime.
                Assert.Equal(300L, es.BackOff.GetUnjitteredMillisecondsForN(0));
            });
        }

        [Fact]
        public void RetryFieldDoesNotRevertAnActiveTemporaryCeiling()
        {
            WithServerAndEventSource(StreamWithRetryLine("300"),
                // A 30s configured ceiling, distinct from the temporary one, so the assertion
                // can tell which is in effect. The reset threshold is long enough that the
                // healthy-op path cannot clear the temporary bounds mid-test.
                c => c.InitialRetryDelay(ConfiguredInitial)
                      .MaxRetryDelay(TimeSpan.FromSeconds(30))
                      .BackoffResetThreshold(TimeSpan.FromSeconds(30)),
                (server, es) =>
                {
                    var sink = new EventSink(es, _testLogging);

                    // The extended regime is already active when the retry: line arrives.
                    es.SetTemporaryRetryDelayBounds(TimeSpan.FromMinutes(5), TimeSpan.FromHours(1));

                    _ = Task.Run(es.StartAsync);
                    sink.ExpectActions(
                        EventSink.OpenedAction(),
                        EventSink.MessageReceivedAction(Marker));

                    Assert.Equal(TimeSpan.FromMilliseconds(300),
                        es.BackOff.GetServerDirectedMinDelay());

                    // The directed value replaces the minimum only. The ceiling stays the
                    // temporary one, so reconnection is still bounded by 1hr rather than
                    // dropping back to the configured 30s.
                    Assert.Equal(TimeSpan.FromHours(1), es.BackOff.GetMaximumDelay());
                    Assert.Equal(3600000L, es.BackOff.GetUnjitteredMillisecondsForN(14));
                });
        }

        // Pins the documented contract on ClearTemporaryRetryDelayBounds: it restores the
        // configured ceiling but must not discard a server-directed minimum.
        [Fact]
        public void RetryFieldSurvivesClearTemporaryRetryDelayBounds()
        {
            WithRetryLine("300", es =>
            {
                es.SetTemporaryRetryDelayBounds(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10));
                es.ClearTemporaryRetryDelayBounds();

                Assert.Equal(TimeSpan.FromMilliseconds(300),
                    es.BackOff.GetServerDirectedMinDelay());
                Assert.Equal(300L, es.BackOff.GetUnjitteredMillisecondsForN(0));

                // The ceiling, unlike the minimum, is genuinely restored.
                Assert.Equal(ConfiguredMax, es.BackOff.GetMaximumDelay());
            });
        }

        [Fact]
        public void RetryFieldAboveOneHourIsClampedToOneHour()
        {
            // ConfiguredMax is 24 hours so the wire cap, not the ceiling, is the bound under test.
            WithRetryLine("7200000", es =>
                Assert.Equal(TimeSpan.FromMilliseconds(Constants.MaxServerDirectedRetryDelayMillis),
                    es.BackOff.GetServerDirectedMinDelay()));
        }

        // A server can send any number that parses as a long, which is a far wider range than
        // TimeSpan can represent. The clamp has to happen while the value is still an integer;
        // otherwise constructing the TimeSpan throws before any cap can apply, and the connection
        // is torn down every time the line is read without the directive being applied at all.
        [Theory]
        [InlineData("1000000000000000")]      // 1e15 ms, just past TimeSpan.MaxValue
        [InlineData("9223372036854775807")]   // long.MaxValue
        public void HugeRetryFieldIsClampedRatherThanThrowing(string value)
        {
            WithRetryLine(value, es =>
                Assert.Equal(TimeSpan.FromMilliseconds(Constants.MaxServerDirectedRetryDelayMillis),
                    es.BackOff.GetServerDirectedMinDelay()));
        }

        [Theory]
        [InlineData("-1000000000000000")]
        [InlineData("-9223372036854775808")]  // long.MinValue
        public void HugeNegativeRetryFieldIsClampedRatherThanThrowing(string value)
        {
            WithRetryLine(value, es =>
                Assert.Equal(TimeSpan.Zero, es.BackOff.GetServerDirectedMinDelay()));
        }

        [Fact]
        public void RetryFieldChangesTheActualWaitBeforeReconnecting()
        {
            var handler = Handlers.Sequential(
                StartStream()
                    .Then(Handlers.WriteChunkString("retry: 400\n"))
                    .Then(WriteEvent(Marker)),        // first stream then ends, forcing a reconnect
                StartStream().Then(LeaveStreamOpen())
                );

            WithServerAndEventSource(handler,
                // 10ms configured initial delay: without the directed value the reconnect would
                // land at roughly 5-10ms, two orders of magnitude below the assertion.
                c => c.InitialRetryDelay(TimeSpan.FromMilliseconds(10))
                      .MaxRetryDelay(TimeSpan.FromSeconds(30))
                      .BackoffResetThreshold(TimeSpan.FromSeconds(30)),
                (server, es) =>
                {
                    var sink = new EventSink(es, _testLogging);
                    _ = Task.Run(es.StartAsync);

                    server.Recorder.RequireRequest();
                    var timer = Stopwatch.StartNew();

                    // Receiving the event proves the preceding retry: line was parsed.
                    sink.ExpectActions(
                        EventSink.OpenedAction(),
                        EventSink.MessageReceivedAction(Marker));

                    server.Recorder.RequireRequest();
                    timer.Stop();

                    // retry: 400 puts the floor at 200ms; 150ms leaves room for timer skew while
                    // staying far above the 10ms the configured delay would have produced.
                    Assert.True(timer.Elapsed >= TimeSpan.FromMilliseconds(150),
                        $"reconnected after {timer.ElapsedMilliseconds}ms; retry: 400 should have "
                        + "delayed it by at least 200ms, so the directed value did not reach the wait");
                });
        }

        [Fact]
        public void RetryFieldOfZeroDisablesBackoff()
        {
            WithRetryLine("0", es =>
            {
                Assert.Equal(TimeSpan.Zero, es.BackOff.GetServerDirectedMinDelay());
                Assert.Equal(0L, es.BackOff.GetUnjitteredMillisecondsForN(0));
                Assert.Equal(TimeSpan.Zero, es.BackOff.GetNextBackOff());
            });
        }

        [Theory]
        [InlineData("7000L")]
        [InlineData("3.5")]
        [InlineData("1e3")]
        [InlineData("1,000")]
        [InlineData("")]
        [InlineData("abc")]
        public void UnparseableRetryFieldIsIgnored(string value)
        {
            WithRetryLine(value, es =>
                Assert.Null(es.BackOff.GetServerDirectedMinDelay()));
        }

        [Fact]
        public void NegativeRetryFieldClampsToZeroAndDisablesBackoff()
        {
            // The RETRY specification imposes no floor on server-directed values, so a negative
            // one clamps to zero and zero means reconnect without waiting.
            WithRetryLine("-5", es =>
            {
                Assert.Equal(TimeSpan.Zero, es.BackOff.GetServerDirectedMinDelay());
                Assert.Equal(TimeSpan.Zero, es.BackOff.GetNextBackOff());
            });
        }

        [Fact]
        public void RetryFieldWithLeadingSignIsAccepted()
        {
            WithRetryLine("+5", es =>
                Assert.Equal(TimeSpan.FromMilliseconds(5),
                    es.BackOff.GetServerDirectedMinDelay()));
        }

        [Fact]
        public void RetryFieldSurvivesSustainedHealthyConnection()
        {
            // A zero reset threshold makes the healthy-op reset fire deterministically on the
            // reconnect. The directed value must survive it.
            // The first stream must end rather than stay open, so that a reconnect happens and
            // the healthy-op reset runs.
            var handler = Handlers.Sequential(
                StartStream()
                    .Then(Handlers.WriteChunkString("retry: 300\n"))
                    .Then(WriteEvent(Marker)),
                StartStream().Then(WriteEvent(Marker)).Then(LeaveStreamOpen())
                );

            WithServerAndEventSource(handler,
                c => c.InitialRetryDelay(ConfiguredInitial).MaxRetryDelay(ConfiguredMax)
                      .BackoffResetThreshold(TimeSpan.Zero),
                (server, es) =>
                {
                    _ = new EventSink(es, _testLogging);
                    _ = Task.Run(es.StartAsync);

                    server.Recorder.RequireRequest();
                    server.Recorder.RequireRequest();

                    Assert.Equal(TimeSpan.FromMilliseconds(300),
                        es.BackOff.GetServerDirectedMinDelay());
                });
        }

        [Fact]
        public void LaterRetryFieldReplacesEarlier()
        {
            var handler = StartStream()
                .Then(Handlers.WriteChunkString("retry: 300\n"))
                .Then(Handlers.WriteChunkString("retry: 700\n"))
                .Then(WriteEvent(Marker))
                .Then(LeaveStreamOpen());

            WithServerAndEventSource(handler,
                c => c.InitialRetryDelay(ConfiguredInitial).MaxRetryDelay(ConfiguredMax),
                (server, es) =>
                {
                    var sink = new EventSink(es, _testLogging);
                    _ = Task.Run(es.StartAsync);

                    sink.ExpectActions(
                        EventSink.OpenedAction(),
                        EventSink.MessageReceivedAction(Marker));

                    Assert.Equal(TimeSpan.FromMilliseconds(700),
                        es.BackOff.GetServerDirectedMinDelay());
                });
        }
    }
}
