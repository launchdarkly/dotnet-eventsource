using System;

namespace LaunchDarkly.EventSource.Background
{
    /// <summary>
    /// Parameter type for the <see cref="BackgroundEventSource.Error"/> event.
    /// </summary>
    public class ExceptionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the <see cref="System.Exception"/> that represents the error that occurred.
        /// </summary>
        /// <value>
        /// The exception.
        /// </value>
        public Exception Exception { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExceptionEventArgs"/> class.
        /// </summary>
        /// <param name="ex">A <see cref="System.Exception"/> that represents the error that occurred.</param>
        public ExceptionEventArgs(Exception ex)
        {
            Exception = ex;
        }
    }
}
