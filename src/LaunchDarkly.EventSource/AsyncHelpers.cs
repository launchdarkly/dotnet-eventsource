using System;
using System.Threading;
using System.Threading.Tasks;

namespace LaunchDarkly.EventSource
{
    internal static class AsyncHelpers
    {
        // General-purpose timeout logic that uses a timed CancellationToken instead of
        // Task.Delay. The cancellationToken that is passed in is for explicitly cancelling
        // the operation from elsewhere; DoWithTimeout wraps this to create a token that can
        // also be cancelled by a timeout, and passes that token to taskFn.
        internal static async Task<T> DoWithTimeout<T>(
            TimeSpan timeout,
            CancellationToken cancellationToken,
            Func<CancellationToken, Task<T>> taskFn
            )
        {
            var timeoutCancellation = new CancellationTokenSource(timeout);
            var combinedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCancellation.Token);
            Task<T> task = null;
            try
            {
                task = taskFn(combinedCancellation.Token);
                return await task;
            }
            catch (OperationCanceledException)
            {
                SuppressExceptions(task);
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                throw new ReadTimeoutException();
            }
        }

        // Take a task that can't be terminated early with a CancellationToken, and wrap it in a task
        // that can. If you cancel the wrapped task, the inner task will still continue, but we're
        // assuming that in these situations we'll eventually be closing the underlying socket which
        // will terminate any pending I/O.
        internal static Task<T> AllowCancellation<T>(Task<T> task, CancellationToken cancellationToken)
        {
            var ret = task.ContinueWith(
                completedTask =>
                {
                    if (cancellationToken.IsCancellationRequested && completedTask.IsFaulted)
                    {
                        _ = completedTask.Exception; // prevents this from being an unobserved exception
                    }
                    return completedTask.GetAwaiter().GetResult();
                },
                cancellationToken,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );
            ret.ContinueWith(
                (completedTask, _) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        SuppressExceptions(task);
                    }
                },
                null,
                TaskScheduler.Default
                );
            return ret;
        }

        /// <summary>
        /// Adds a continuation to a task so that if the task throws an uncaught exception, the exception
        /// is not "unobserved" (which can be a fatal error in some versions of .NET). We must do this
        /// whenever we're going to discard a task without awaiting it, if there's any possibility that
        /// it could throw an exception.
        /// </summary>
        /// <param name="task">a task we are not going to await</param>
        internal static void SuppressExceptions(Task task)
        {
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    // Simply accessing the Exception property makes this exception observed.
                    var e = t.Exception;
                }
            });
        }
    }
}
