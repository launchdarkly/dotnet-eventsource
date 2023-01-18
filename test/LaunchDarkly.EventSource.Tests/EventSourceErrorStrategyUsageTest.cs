using System;
using System.Threading.Tasks;
using LaunchDarkly.EventSource.Exceptions;
using LaunchDarkly.EventSource.Events;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.EventSource.TestHelpers;

namespace LaunchDarkly.EventSource
{
	public class EventSourceErrorStrategyUsageTest : BaseTest
    {
        private static readonly Exception FakeHttpError = new StreamHttpErrorException(503);

        public EventSourceErrorStrategyUsageTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public async Task TaskStartThrowsIfConnectFailsAndStrategyReturnsThrow()
        {
            await WithMockConnectEventSource(
                mock => mock.ConfigureRequests(MockConnectStrategy.RejectConnection(FakeHttpError)),
                c => c.ErrorStrategy(ErrorStrategy.AlwaysThrow)
                    .InitialRetryDelay(TimeSpan.Zero),
                async (mock, es) =>
                {
                    var ex = await Assert.ThrowsAnyAsync<Exception>(() => es.StartAsync());
                    Assert.Equal(FakeHttpError, ex);
                }
            );
        }

        [Fact]
        public async Task TaskStartRetriesIfConnectFailsAndStrategyReturnsContinue()
        {
            await WithMockConnectEventSource(
                mock => mock.ConfigureRequests(
                    MockConnectStrategy.RejectConnection(FakeHttpError),
                    MockConnectStrategy.RejectConnection(FakeHttpError),
                    MockConnectStrategy.RespondWithStream()
                    ),
                c => c.ErrorStrategy(ErrorStrategy.AlwaysContinue)
                    .InitialRetryDelay(TimeSpan.Zero),
                async (mock, es) =>
                {
                    await es.StartAsync();
                }
            );
        }

        [Fact]
        public async Task ImplicitStartFromReadAnyEventReturnsFaultEventIfStrategyReturnsContinue()
        {
            await WithMockConnectEventSource(
                mock => mock.ConfigureRequests(
                    MockConnectStrategy.RejectConnection(FakeHttpError),
                    MockConnectStrategy.RespondWithStream()
                    ),
                c => c.ErrorStrategy(ErrorStrategy.AlwaysContinue)
                    .InitialRetryDelay(TimeSpan.Zero),
                async (mock, es) =>
                {
                    Assert.Equal(new FaultEvent(FakeHttpError),
                        await es.ReadAnyEventAsync());
                    Assert.Equal(new StartedEvent(), await es.ReadAnyEventAsync());
                }
            );
        }

        [Fact]
        public async Task ErrorStrategyIsUpdatedForEachRetryDuringStart()
        {
            var fakeError2 = new Exception("the final error");
            var continueFirstTimeButThenThrow = new ContinueFirstTimeStrategy();
            await WithMockConnectEventSource(
                mock => mock.ConfigureRequests(
                    MockConnectStrategy.RejectConnection(FakeHttpError),
                    MockConnectStrategy.RejectConnection(fakeError2),
                    MockConnectStrategy.RespondWithStream()
                    ),
                c => c.ErrorStrategy(continueFirstTimeButThenThrow)
                    .InitialRetryDelay(TimeSpan.Zero),
                async (mock, es) =>
                {
                    var ex = await Assert.ThrowsAnyAsync<Exception>(() => es.StartAsync());
                    Assert.Equal(fakeError2, ex);
                }
            );
        }

        class ContinueFirstTimeStrategy : ErrorStrategy
        {
            public override Result Apply(Exception exception) =>
                new Result { Action = Action.Continue, Next = ErrorStrategy.AlwaysThrow };
        }

        [Fact]
        public async Task ReadThrowsIfReadFailsAndStrategyReturnsThrow()
        {
            await WithMockConnectEventSource(
                mock => mock.ConfigureRequests(
                    MockConnectStrategy.RespondWithDataAndThenEnd("data:\n\n")
                    ),
                c => c.ErrorStrategy(ErrorStrategy.AlwaysThrow)
                    .InitialRetryDelay(TimeSpan.Zero),
                async (mock, es) =>
                {
                    await es.StartAsync();
                    await es.ReadMessageAsync();
                    await Assert.ThrowsAnyAsync<StreamClosedByServerException>(() => es.ReadMessageAsync());
                }
            );
        }

        [Fact]
        public async Task ReadRetriesIfReadFailsAndStrategyReturnsContinue()
        {
            await WithMockConnectEventSource(
                mock => mock.ConfigureRequests(
                    MockConnectStrategy.RespondWithDataAndThenEnd("data:a\n\n"),
                    MockConnectStrategy.RespondWithDataAndThenEnd("data:b\n\n")
                    ),
                c => c.ErrorStrategy(ErrorStrategy.AlwaysContinue)
                    .InitialRetryDelay(TimeSpan.Zero),
                async (mock, es) =>
                {
                    await es.StartAsync();
                    await es.ReadMessageAsync();
                    Assert.Equal(new MessageEvent(MessageEvent.DefaultName, "b", MockConnectStrategy.MockOrigin),
                        await es.ReadMessageAsync());
                }
            );
        }

        [Fact]
        public async Task ErrorStrategyIsUpdatedForEachRetryDuringRead()
        {
            var continueFirstTimeButThenThrow = new ContinueFirstTimeStrategy();
            await WithMockConnectEventSource(
                mock => mock.ConfigureRequests(
                    MockConnectStrategy.RespondWithDataAndThenEnd("data:a\n\n"),
                    MockConnectStrategy.RejectConnection(FakeHttpError),
                    MockConnectStrategy.RespondWithDataAndThenEnd("data:b\n\n")
                    ),
                c => c.ErrorStrategy(continueFirstTimeButThenThrow)
                    .InitialRetryDelay(TimeSpan.Zero),
                async (mock, es) =>
                {
                    await es.ReadMessageAsync();
                    var ex = await Assert.ThrowsAnyAsync<Exception>(() => es.ReadMessageAsync());
                    Assert.Equal(FakeHttpError, ex);
                }
            );
        }
    }
}

