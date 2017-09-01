namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// Represents the state of the connection in the <see cref="EventSource"/> class.
    /// </summary>
    public enum ReadyState
    {
        /// <summary>
        /// The initial state of the connection.
        /// </summary>
        Raw,
        /// <summary>
        /// The connection has not yet been established, or it was closed and is reconnecting.
        /// </summary>
        Connecting,
        /// <summary>
        /// The connection is open and is processing events as it receives them.
        /// </summary>
        Open,
        /// <summary>
        /// The connection is closed. This could also occur when an error is received.
        /// </summary>
        Closed,
        /// <summary>
        /// The connection has been shutdown explicitly by the consumer using the <see cref="EventSource.Close"/> method.
        /// </summary>
        Shutdown
    }
}
