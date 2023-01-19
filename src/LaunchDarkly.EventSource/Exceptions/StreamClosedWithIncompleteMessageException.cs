using System;
namespace LaunchDarkly.EventSource.Exceptions
{
    /// <summary>
    /// An exception indicating that the stream connection was lost before reaching
    /// the end of the current message (that is, a blank line).
    /// </summary>
    /// <remarks>
    /// This applies only if you have enabled <see cref="ConfigurationBuilder.StreamEventData(bool)"/>
    /// mode and are reading the data as a stream.
    /// </remarks>
    public class StreamClosedWithIncompleteMessageException : StreamException
    {
        /// <summary>
        /// Creates an instance.
        /// </summary>
        public StreamClosedWithIncompleteMessageException() :
            base(Resources.StreamClosedByServer)
        { }
    }
}
