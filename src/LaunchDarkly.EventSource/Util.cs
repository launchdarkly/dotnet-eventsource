using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LaunchDarkly.EventSource
{
    internal class Util
    {
        /// <summary>
        /// Adds a continuation to a task so that if the task throws an uncaught exception, the exception
        /// is not "unobserved" (which can be a fatal error in some versions of .NET). We must do this
        /// whenever we're going to discard a task without awaiting it, if there's any possibility that
        /// it could throw an exception.
        /// </summary>
        /// <param name="task">a task we are not going to await</param>
        public static void SuppressExceptions(Task task)
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
