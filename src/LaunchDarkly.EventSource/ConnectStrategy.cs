using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.EventSource.Events;
using LaunchDarkly.EventSource.Exceptions;
using LaunchDarkly.Logging;

using static System.Net.WebRequestMethods;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// An abstraction of how EventSource should obtain an input stream.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default implementation is <see cref="HttpConnectStrategy"/>, which makes HTTP
    /// requests. To customize the HTTP behavior, you can use methods of <see cref="HttpConnectStrategy"/>:
    /// </para>
    /// <example><code>
    ///     var config = Configuration.Builder(
    ///         ConnectStrategy.Http(streamUri)
    ///             .Header("name", "value")
    ///             .ReadTimeout(TimeSpan.FromMinutes(1))
    ///     );
    /// </code></example>>
    /// <para>
    /// Or, if you want to consume an input stream from some other source, you can
    /// create your own subclass of ConnectStrategy.
    /// </para>
    /// <para>
    /// Instances of this class should be immutable and not contain any state that
    /// is specific to one active stream.The {@link ConnectStrategy.Client}
    /// that they produce is stateful and belongs to a single EventSource.
    /// </para>
    /// </remarks>
    public abstract class ConnectStrategy
    {
        /// <summary>
        /// The origin URI that should be included in every <see cref="MessageEvent"/>.
        /// </summary>
        /// <remarks>
        /// The SSE specification dictates that every message should have an origin
        /// property representing the stream it came from. In the default HTTP
        /// implementation, this is simply the stream URI; other implementations of
        /// ConnectStrategy can set it to whatever they want.
        /// </remarks>
        public abstract Uri Origin { get; }

        /// <summary>
        /// Creates a client instance.
        /// </summary>
        /// <remarks>
        /// This is called once when an EventSource is created. The EventSource
        /// retains the returned Client and uses it to perform all subsequent
        /// connection attempts.
        /// </remarks>
        /// <param name="logger">the logger belonging to EventSource</param>
        /// <returns>a <see cref="Client"/></returns>
        public abstract Client CreateClient(Logger logger);

        /// <summary>
        /// An object provided by <see cref="ConnectStrategy"/> that is retained by a
        /// single EventSource instance to perform all connection attempts by that instance.
        /// </summary>
        public abstract class Client : IDisposable
        {
            /// <summary>
            /// The parameter type of <see cref="ConnectAsync(Params)"/>.
            /// </summary>
            public struct Params
            {
                /// <summary>
                /// A CancellationToken to be used when making a connection.
                /// </summary>
                public CancellationToken CancellationToken { get; set; }

                /// <summary>
                /// The current value of <see cref="EventSource.LastEventId"/>. This should
                /// be sent to the server to support resuming an interrupted stream.
                /// </summary>
                public string LastEventId { get; set; }
            }

            /// <summary>
            /// The return type of <see cref="ConnectAsync(Params)"/>.
            /// </summary>
            public class Result : IDisposable
            {
                /// <summary>
                /// The input stream that EventSource should read from.
                /// </summary>
                public Stream Stream { get; }

                /// <summary>
                /// If non-null, indicates that EventSource should impose its own
                /// timeout on reading the stream.
                /// </summary>
                /// <remarks>
                /// Due to platform limitations, it may not be possible to implement a
                /// read timeout within the stream itself. Returning a non-null value
                /// here tells EventSource to add its own timeout logic using a
                /// CancellationToken.
                /// </remarks>
                public TimeSpan? ReadTimeout { get; }

                private IDisposable _closer;

                /// <summary>
                /// Creates an instance.
                /// </summary>
                /// <param name="stream">see <see cref="Stream"/></param>
                /// <param name="readTimeout">see <see cref="ReadTimeout"/></param>
                /// <param name="closer">if non-null, this object's <see cref="IDisposable.Dispose"/>
                /// method will be called whenever the current connection is stopped</param>
                public Result(
                    Stream stream,
                    TimeSpan? readTimeout = null,
                    IDisposable closer = null
                    )
                {
                    Stream = stream;
                    ReadTimeout = readTimeout;
                    _closer = closer;
                }

                /// <summary>
                /// Releases any resources related to this connection.
                /// </summary>
                public void Dispose()
                {
                    _closer?.Dispose();
                }
            }

            /// <summary>
            /// Attempts to connect to a stream.
            /// </summary>
            /// <param name="parameters">parameters for this connection attempt</param>
            /// <returns>the result if successful</returns>
            /// <exception cref="IOException">if the connection fails</exception>
            /// <exception cref="StreamException">if there is some other type of error,
            /// such as an invalid HTTP status</exception>
            public abstract Task<Result> ConnectAsync(Params parameters);

            /// <inheritdoc/>
            public abstract void Dispose();
        }

        /// <summary>
        /// Returns the default HTTP implementation, specifying a stream URI.
        /// </summary>
        /// <remarks>
        /// <para>
        /// To specify custom HTTP behavior, call <see cref="HttpConnectStrategy"/> methods
        /// on the returned object to obtain a modified instance:
        /// </para>
        /// <example><code>
        ///
        /// </code></example>>
        /// </remarks>
        /// <param name="uri">the stream URI</param>
        /// <returns>a configurable <see cref="HttpConnectStrategy"/></returns>
        public static HttpConnectStrategy Http(Uri uri) => new HttpConnectStrategy(uri);
    }
}
