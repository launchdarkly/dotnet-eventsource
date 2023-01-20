using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using LaunchDarkly.EventSource.Events;
using LaunchDarkly.EventSource.Exceptions;
using LaunchDarkly.Logging;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// A standard Builder pattern for constructing a <see cref="Configuration"/> instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Initialize a builder by calling one of the <see cref="Configuration"/> factory methods
    /// such as <see cref="Configuration.Builder(Uri)"/>. All properties are initially set to
    /// defaults. Use the builder's setter methods to modify any desired properties; setter
    /// methods can be chained. Then call <c>Build()</c> to construct the final immutable
    /// <c>Configuration</c>.
    /// </para>
    /// <para>
    /// All setter methods will throw <c>ArgumentException</c> if called with an invalid value,
    /// so it is never possible for <c>Build()</c> to fail.
    /// </para>
    /// </remarks>
    public class ConfigurationBuilder
    {
        #region Private Fields

        internal TimeSpan _initialRetryDelay = Configuration.DefaultInitialRetryDelay;
        internal ConnectStrategy _connectStrategy;
        internal ErrorStrategy _errorStrategy;
        internal HashSet<string> _expectFields;
        internal string _lastEventId;
        internal ILogAdapter _logAdapter;
        internal Logger _logger;
        internal RetryDelayStrategy _retryDelayStrategy;
        internal TimeSpan _retryDelayResetThreshold = Configuration.DefaultRetryDelayResetThreshold;
        internal bool _streamEventData;

        #endregion

        #region Constructor

        internal ConfigurationBuilder(ConnectStrategy connectStrategy)
        {
            if (connectStrategy is null)
            {
                throw new ArgumentNullException("connectStrategy");
            }
            _connectStrategy = connectStrategy;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Constructs a <see cref="Configuration"/> instance based on the current builder properies.
        /// </summary>
        /// <returns>the configuration</returns>
        public Configuration Build() =>
            new Configuration(this);

        /// <summary>
        /// Specifies a strategy for determining whether to handle errors transparently
        /// or throw them as exceptions.
        /// </summary>
        /// <remarks>
        /// By default, any failed connection attempt, or failure of an existing connection,
        /// will be thrown as a {@link StreamException} when you try to use the stream. You
        /// may instead use alternate <see cref="LaunchDarkly.EventSource.ErrorStrategy"/>
        /// implementations, such as <see cref="LaunchDarkly.EventSource.ErrorStrategy.AlwaysContinue"/>,
        /// or a custom implementation, to allow EventSource to continue after an error.
        /// </remarks>
        /// <param name="errorStrategy">the object that will control error handling;
        /// if null, defaults to <see cref="LaunchDarkly.EventSource.ErrorStrategy.AlwaysThrow"/></param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder ErrorStrategy(ErrorStrategy errorStrategy)
        {
            this._errorStrategy = errorStrategy ?? LaunchDarkly.EventSource.ErrorStrategy.AlwaysThrow;
            return this;
        }

        /// <summary>
        /// Specifies that the application expects the server to send certain fields in every event.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This setting makes no difference unless you have enabled <see cref="StreamEventData(bool)"/>
        /// mode. In that case, it causes EventSource to only use the streaming data mode for an event
        /// if the specified fields have already been received; otherwise, it will buffer the whole
        /// event (as if <see cref="StreamEventData(bool)"/> had not been enabled), to ensure that those
        /// fields are not lost if they appear after the <c>data:</c> field.
        /// </para>
        /// <para>
        /// For instance, if you had called <c>ExpectFields("event")</c>, then EventSource would be able
        /// to use streaming data mode for the following SSE response--
        /// </para>
        /// <code>
        /// event: hello
        /// data: here is some very long streaming data
        /// </code>
        /// <para>
        /// --but it would buffer the full event if the server used the opposite order:
        /// </para>
        /// <code>
        /// data: here is some very long streaming data
        /// event: hello
        /// </code>
        /// <para>
        /// Such behavior is not automatic because in some applications, there might never be an
        /// <c>event:</c> field, and EventSource has no way to anticipate this.
        /// </para>
        /// <para>
        /// Specifying any field names other than <c>"event"</c> and <c>"id"</c> has no effect, since
        /// the only fields defined in SSE are <c>event</c>, <c>id</c>, and <c>data</c> (not counting
        /// <c>retry</c>, since that is a directive to the client rather than part of a message).
        /// </para>
        /// </remarks>
        /// <param name="fieldNames">a list of SSE field names (case-sensitive)</param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder ExpectFields(params string[] fieldNames)
        {
            _expectFields = fieldNames is null ? null : new HashSet<string>(fieldNames);
            return this;
        }

        /// <summary>
        /// Sets the initial amount of time to wait before attempting to reconnect to the EventSource API.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the connection fails more than once, the retry delay will increase from this value using
        /// a backoff algorithm.
        /// </para>
        /// <para>
        /// The default value is <see cref="Configuration.DefaultInitialRetryDelay"/>. Negative values
        /// are changed to zero.
        /// </para>
        /// <para>
        /// The actual duration of each delay will vary slightly because there is a random jitter
        /// factor to avoid clients all reconnecting at once.
        /// </para>
        /// </remarks>
        /// <param name="initialRetryDelay">the initial retry delay</param>
        /// <returns>the builder</returns>
        /// <seealso cref="DefaultRetryDelayStrategy"/>
        public ConfigurationBuilder InitialRetryDelay(TimeSpan initialRetryDelay)
        {
            _initialRetryDelay = FiniteTimeSpan(initialRetryDelay);
            return this;
        }

        /// <summary>
        /// Sets the last event identifier.
        /// </summary>
        /// <remarks>
        /// Setting this value will cause EventSource to add a "Last-Event-ID" header in its HTTP request.
        /// This normally corresponds to the <see cref="MessageEvent.LastEventId"/> field of a previously
        /// received event.
        /// </remarks>
        /// <param name="lastEventId">the event identifier</param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder LastEventId(string lastEventId)
        {
            _lastEventId = lastEventId;
            return this;
        }

        /// <summary>
        /// Sets the logging implementation to be used for all EventSource log output.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This uses the <c>ILogAdapter</c> abstraction from the <c>LaunchDarkly.Logging</c> library,
        /// which provides several basic implementations such as <c>Logs.ToConsole</c> and an integration
        /// with the .NET Core logging framework. For more about this and about adapters to other logging
        /// frameworks, see <a href="https://github.com/launchdarkly/dotnet-logging"><c>LaunchDarkly.Logging</c></a>.
        /// </para>
        /// <para>
        /// <c>LaunchDarkly.Logging</c> defines logging levels of Debug, Info, Warn, and Error. If you do not
        /// want detailed Debug-level logging, use the <c>Level()</c> modifier to set a minimum level of Info
        /// or above, as shown in the code example (unless you are using an adapter to another logging
        /// framework that has its own way of doing log filtering).
        /// </para>
        /// <para>
        /// Log messages will use <see cref="Configuration.DefaultLoggerName"/> as the logger name. If you
        /// want to specify a different logger name, use <see cref="Logger(Logging.Logger)"/>.
        /// </para>
        /// <para>
        /// If you don't specify <see cref="LogAdapter(ILogAdapter)"/> or <see cref="Logger(Logging.Logger)"/>,
        /// EventSource will not do any logging.
        /// </para>
        /// </remarks>
        /// <example>
        ///     using LaunchDarkly.Logging;
        ///     
        ///     // Send log output to the console (standard error), suppressing Debug messages
        ///     var config = new ConfigurationBuilder(uri).
        ///         LogAdapter(Logs.ToConsole.Level(LogLevel.Info)).
        ///         Build();
        /// </example>
        /// <param name="logAdapter">a <c>LaunchDarkly.Logging.ILogAdapter</c></param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder LogAdapter(ILogAdapter logAdapter)
        {
            _logAdapter = logAdapter;
            return this;
        }

        /// <summary>
        /// Sets a custom logger to be used for all EventSource log output.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This uses the <c>Logger</c> type from the <c>LaunchDarkly.Logging</c> library,
        /// which provides several basic implementations such as <c>Logs.ToConsole</c> and an integration
        /// with the .NET Core logging framework. For more about this and about adapters to other logging
        /// frameworks, see <a href="https://github.com/launchdarkly/dotnet-logging"><c>LaunchDarkly.Logging</c></a>.
        /// </para>
        /// <para>
        /// If you don't specify <see cref="LogAdapter(ILogAdapter)"/> or <see cref="Logger(Logging.Logger)"/>,
        /// EventSource will not do any logging.
        /// </para>
        /// </remarks>
        /// <example>
        ///     using LaunchDarkly.Logging;
        ///     
        ///     // Define a logger that sends output to the console (standard output), suppressing
        ///     // Debug messages, and using a logger name of "EventStream"
        ///     var logger = Logs.ToConsole.Level(LogLevel.Info).Logger("EventStream");
        ///     var config = new ConfigurationBuilder(uri).
        ///         Logger(logger).
        ///         Build();
        /// </example>
        /// <param name="logger">a <c>LaunchDarkly.Logging.Logger</c> instance</param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder Logger(Logger logger)
        {
            _logger = logger;
            return this;
        }

        /// <summary>
        /// Specifies a strategy for determining the retry delay after an error.
        /// </summary>
        /// <remarks>
        /// Whenever EventSource tries to start a new connection after a stream failure,
        /// it delays for an amount of time that is determined by two parameters: the
        /// base retry delay (<see cref="InitialRetryDelay(TimeSpan)"/>), and the retry
        /// delay strategy which transforms the base retry delay in some way. The default
        /// behavior is to apply an exponential backoff and jitter. You may instead use a
        /// modified version of <see cref="DefaultRetryDelayStrategy"/> to customize the
        /// backoff and jitter, or a custom implementation with any other logic.
        /// </remarks>
        /// <param name="retryDelayStrategy">the object that will control retry delays; if
        /// null, defaults to <see cref="LaunchDarkly.EventSource.RetryDelayStrategy.Default"/></param>
        /// <returns>the builder</returns>
        public ConfigurationBuilder RetryDelayStrategy(RetryDelayStrategy retryDelayStrategy)
        {
            _retryDelayStrategy = retryDelayStrategy;
            return this;
        }

        /// <summary>
        /// Sets the amount of time a connection must stay open before the EventSource
        /// resets its delay strategy.
        /// </summary>
        /// <remarks>
        /// When using the default strategy (see <see cref="RetryDelayStrategy"/>), this means
        /// that the delay before each reconnect attempt will be greater than the last delay
        /// unless the current connection lasted longer than the threshold, in which case the
        /// delay will start over at the initial minimum value. This prevents long delays from
        /// occurring on connections that are only rarely restarted.
        /// </remarks>
        /// <param name="retryDelayResetThreshold">the threshold time</param>
        /// <returns>the builder</returns>
        /// <see cref="Configuration.DefaultRetryDelayResetThreshold"/>
        public ConfigurationBuilder RetryDelayResetThreshold(TimeSpan retryDelayResetThreshold)
        {
            _retryDelayResetThreshold = FiniteTimeSpan(retryDelayResetThreshold);
            return this;
        }

        /// <summary>
        /// Specifies whether EventSource should return a <see cref="MessageEvent"/> to the
        /// handler as soon as it receives the beginning of the event data, allowing the caller
        /// to read the data incrementally.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The default for this property is <see langword="false"/>, meaning that EventSource
        /// will always read the entire event into memory before returning it.
        /// </para>
        /// <para>
        /// If you set it to <see langword="true"/>, it will instead return the event as soon as
        /// it sees a <c>data</c> field-- setting <see cref="MessageEvent.DataStream"/> to a
        /// <see cref="System.IO.Stream"/> that reads directly from the data as it arrives. The
        /// EventSource will perform any necessary parsing under the covers, so that for instance
        /// if there are multiple <c>data:</c> lines in the event, the Stream will emit a newline
        /// character between each and will omit the <c>data:</c> field names. The Stream will
        /// report "end of stream" as soon as the event is terminated normally by a blank line.
        /// If the stream is closed before normal termination of the event, reading the Stream
        /// will throw a <see cref="StreamClosedWithIncompleteMessageException"/>.
        /// </para>
        /// <para>
        /// This mode is designed for applications that expect very large data items to be
        /// delivered over SSE. Use it with caution, since there are several limitations:
        /// </para>
        /// <list type="bullet">
        /// <item><description>
        /// The <see cref="MessageEvent"/> is constructed as soon as a <c>data:</c> field appears,
        /// so it will only include fields that appeared before that point. In other words, if
        /// the SSE server happens to send <c>data:</c> first and <c>event:</c> second,
        /// <see cref="MessageEvent.Name"/> will not contain the value of the <c>event:</c> field
        /// but will be <see cref="MessageEvent.DefaultName"/> instead. Therefore, you should only
        /// use this mode if the server's behavior is predictable in this regard.
        /// </description></item>
        /// <item><description>
        /// The SSE protocol specifies that an event should be processed only if it is terminated
        /// by a blank line, but in this mode the handler will receive the event as soon as a
        /// <c>data:</c> field appears-- so, if the stream happens to cut off abnormally without a
        /// trailing blank line, technically you will be receiving an incomplete event that should
        /// have been ignored. You will know this has happened if reading from the Stream throws
        /// a <see cref="StreamClosedWithIncompleteMessageException"/>.
        /// </description></item>
        /// </list>
        /// </remarks>
        /// <param name="streamEventData">true if events should be dispatched immediately with
        /// asynchronous data rather than read fully first</param>
        /// <returns>the builder</returns>
        /// <see cref="MessageEvent.DataStream"/>
        public ConfigurationBuilder StreamEventData(bool streamEventData)
        {
            _streamEventData = streamEventData;
            return this;
        }

        #endregion

        #region Private methods

        // Canonicalizes the value so all negative numbers become InfiniteTimeSpan
        internal static TimeSpan TimeSpanCanBeInfinite(TimeSpan t) =>
            t < TimeSpan.Zero ? Timeout.InfiniteTimeSpan : t;

        // Replaces all negative times with zero
        internal static TimeSpan FiniteTimeSpan(TimeSpan t) =>
            t < TimeSpan.Zero ? TimeSpan.Zero : t;

        #endregion
    }
}
