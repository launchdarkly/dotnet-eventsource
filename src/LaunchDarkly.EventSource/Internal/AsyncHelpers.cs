using System;
using System.Threading;
using System.Threading.Tasks;

namespace LaunchDarkly.EventSource.Internal
{
    internal static class AsyncHelpers
    {
        private static readonly TaskFactory _taskFactory = new TaskFactory(CancellationToken.None,
            TaskCreationOptions.None, TaskContinuationOptions.None, TaskScheduler.Default);

        // This procedure for blocking on a Task without using Task.Wait is derived from the MIT-licensed ASP.NET
        // code here: https://github.com/aspnet/AspNetIdentity/blob/master/src/Microsoft.AspNet.Identity.Core/AsyncHelper.cs
        // In general, mixing sync and async code is not recommended, and if done in other ways can result in
        // deadlocks. See: https://stackoverflow.com/questions/9343594/how-to-call-asynchronous-method-from-synchronous-method-in-c
        // Task.Wait would only be safe if we could guarantee that every intermediate Task within the async
        // code had been modified with ConfigureAwait(false), but that is very error-prone.

        internal static T WaitSafely<T>(Func<Task<T>> taskFn) =>
            _taskFactory.StartNew(taskFn)
                .Unwrap()
                .GetAwaiter()
                .GetResult();
        // Note, GetResult does not throw AggregateException so we don't need to post-process exceptions

        // General-purpose timeout logic that uses a timed CancellationToken instead of
        // Task.Delay. The cancellationToken that is passed in is for explicitly cancelling
        // the operation from elsewhere; DoWithTimeout wraps this to create a token that can
        // also be cancelled by a timeout, and passes that token to taskFn.
        //
        // This method will only work if taskFn actually responds to the cancellation token
        // by throwing an OperationCanceledException. If taskFn ignores the token, timeouts
        // will not happen.
        internal static async Task<T> DoWithTimeout<T>(
            TimeSpan timeout,
            CancellationToken cancellationToken,
            Func<CancellationToken, Task<T>> taskFn
            )
        {
            using (var timeoutCancellation = new CancellationTokenSource(timeout))
            {
                using (var combinedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, timeoutCancellation.Token))
                {
                    Task<T> task = null;
                    try
                    {
                        task = taskFn(combinedCancellation.Token);
                        var result = await task;
                        return result;
                    }
                    catch (AggregateException e) when (e.InnerException is OperationCanceledException)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            throw e.InnerException;
                        }
                        throw new ReadTimeoutException();
                    }
                    catch (OperationCanceledException)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        throw new ReadTimeoutException();
                    }
                }
            }
            // We never need to call SuppressExceptions(task) in this method, because the act
            // of awaiting the task - whether it completes normally or throws an exception -
            // makes it "observed". The timeout isn't happening in a different task, it's
            // causing this task to throw an OperationCanceledException, after which point it
            // can't generate any other exceptions.
        }

        // Take a task that can't be terminated early with a CancellationToken, and wrap it in a task
        // that can. If you cancel the wrapped task, the inner task will still continue, but we're
        // assuming that in these situations we'll eventually be closing the underlying socket which
        // will terminate any pending I/O.
        internal static Task<T> AllowCancellation<T>(Task<T> originalTask, CancellationToken cancellationToken)
        {
            var cancellableTask = originalTask.ContinueWith(
                completedTask => completedTask.GetAwaiter().GetResult(),
                cancellationToken,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );

            // Now, if originalTask either completes successfully or fails with an exception, anyone who
            // was waiting on cancellableTask will get the result or the exception.
            //
            // But if cancellationToken gets cancelled, cancellableTask will stop and throw an
            // OperationCancelledException, just as cancellable I/O operation normally would. The
            // caller is no longer blocked - even though originalTask may still be executing.
            //
            // Normally it would be a problem that originalTask could still be hanging around, but
            // in our case if things are getting cancelled it means the connection is going to be
            // closed soon, causing any lingering I/O tasks that use that connection to fail. We
            // just need to use SuppressExceptions to make sure those failures are discarded rather
            // than causing an "unobserved exception" situation.

            cancellableTask.ContinueWith(
                completedTask =>
                {
                    SuppressExceptions(originalTask);
                },
                TaskContinuationOptions.OnlyOnCanceled
                );
            return cancellableTask;
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
            task.ContinueWith(
                t =>
                {
                    // Simply accessing the Exception property makes this exception observed.
                    var e = t.Exception;
                },
                TaskContinuationOptions.OnlyOnFaulted
                );
        }
    }
}
