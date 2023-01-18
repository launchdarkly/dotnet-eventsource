using System;
using System.Net.NetworkInformation;

namespace LaunchDarkly.EventSource.Exceptions
{
    /// <summary>
    /// Base class for all exceptions thrown by EventSource.
    /// </summary>
    public class StreamException : Exception
    {
        /// <summary>
        /// Empty constructor.
        /// </summary>
        public StreamException() { }

        /// <summary>
        /// Constructor with a message.
        /// </summary>
        /// <param name="message">the exception message</param>
        public StreamException(string message) : base(message) { }

        /// <inheritdoc/>
        public override bool Equals(object o) =>
            o != null && o.GetType() == this.GetType();

        /// <inheritdoc/>
        public override int GetHashCode() =>
            GetType().GetHashCode();
    }
}
