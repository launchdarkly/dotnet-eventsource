using System;

namespace LaunchDarkly.EventSource.Exceptions
{
    /// <summary>
    /// An exception indicating that the stream stopped because you explicitly stopped it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exception only happens if you are trying to read from one thread while
    /// <see cref="EventSource.Interrupt()"/> or <see cref="EventSource.Dispose()"/>
    /// is called from another thread.
    /// </para>
    /// </remarks>
    public class StreamClosedByCallerException : StreamException
    {
        /// <summary>
        /// Creates an instance.
        /// </summary>
        public StreamClosedByCallerException() :
            base(Resources.StreamClosedByCaller)
        { }
    }
}
