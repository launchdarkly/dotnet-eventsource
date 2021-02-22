using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.EventSource.Tests
{
    public class AsyncHelpersTest : BaseTest
    {
        public AsyncHelpersTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public async void DoWithTimeoutCompletesNormallyWithoutTimeout()
        {
            var result = await AsyncHelpers.DoWithTimeout(
                TimeSpan.FromSeconds(1),
                new CancellationTokenSource().Token,
                async (token) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(1));
                    token.ThrowIfCancellationRequested();
                    return 3;
                });
            Assert.Equal(3, result);
        }

        [Fact]
        public async void DoWithTimeoutTimesOut()
        {
            await Assert.ThrowsAnyAsync<ReadTimeoutException>(async () =>
            {
                await AsyncHelpers.DoWithTimeout(
                    TimeSpan.FromMilliseconds(1),
                    new CancellationTokenSource().Token,
                    async (token) =>
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(100));
                        token.ThrowIfCancellationRequested();
                        return true;
                    });
            });
        }

        [Fact]
        public async void DoWithTimeoutCanThrowExceptionFromTask()
        {
            await Assert.ThrowsAnyAsync<EventSourceServiceUnsuccessfulResponseException>(async () =>
            {
                await AsyncHelpers.DoWithTimeout<bool>(
                    TimeSpan.FromSeconds(1),
                    new CancellationTokenSource().Token,
                    async (token) =>
                    {
                        await Task.Yield();
                        throw new EventSourceServiceUnsuccessfulResponseException(401);
                    });
            });
        }

        [Fact]
        public async void AllowCancellationCompletesNormally()
        {
            Func<Task<int>> taskFn = async () =>
            {
                await Task.Yield();
                return 3;
            };
            var cancellationTokenSource = new CancellationTokenSource();
            var result = await AsyncHelpers.AllowCancellation(
                taskFn(),
                cancellationTokenSource.Token
                );
            Assert.Equal(3, result);
        }

        [Fact]
        public async void AllowCancellationReturnsEarlyWhenCancelled()
        {
            // Create a task with two signals for test synchronization. It sets the
            // first signal once the task has actually started, and then it waits
            // for the second signal to indicate that we have triggered a cancellation,
            // at which point it waits a little longer and then throws an exception -
            // which should *not* propagate to the caller, and we'll also verify that
            // it does not become an unobserved exception.

            var signal1 = new EventWaitHandle(false, EventResetMode.ManualReset);
            var signal2 = new EventWaitHandle(false, EventResetMode.ManualReset);
            Func<Task<int>> taskFn = async () =>
            {
                signal1.Set();
                await Task.Delay(TimeSpan.FromMilliseconds(50));
                Assert.True(signal2.WaitOne(TimeSpan.FromSeconds(1)));
                await Task.Yield();
                await Task.Delay(TimeSpan.FromMilliseconds(50));
                throw new Exception("this should NOT become an unobserved exception");
            };

            UnobservedTaskExceptionEventArgs receivedUnobservedException = null;
            EventHandler<UnobservedTaskExceptionEventArgs> exceptionHandler = (object sender, UnobservedTaskExceptionEventArgs e) =>
            {
                e.SetObserved();
                receivedUnobservedException = e;
            };
            TaskScheduler.UnobservedTaskException += exceptionHandler;

            try
            {
                var cancellationTokenSource = new CancellationTokenSource();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                {
                    var t = Task.Run(() => AsyncHelpers.AllowCancellation(
                        taskFn(),
                        cancellationTokenSource.Token
                        ));
                    Assert.True(signal1.WaitOne(TimeSpan.FromSeconds(1)));
                    cancellationTokenSource.Cancel();
                    signal2.Set();
                    await t;
                });

                // Wait a little while and then run the finalizer so that if the task did not
                // get cleaned up properly, the exception it threw will show up as an unobserved
                // exception.
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                GC.Collect();
                GC.WaitForPendingFinalizers();

                Assert.Null(receivedUnobservedException);
            }
            finally
            {
                TaskScheduler.UnobservedTaskException -= exceptionHandler;
            }
        }
    }
}
