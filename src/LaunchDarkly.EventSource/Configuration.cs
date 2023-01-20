using System;
using System.Collections.Generic;
using LaunchDarkly.Logging;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// An immutable class containing configuration properties for <see cref="EventSource"/>.
    /// </summary>
    /// <seealso cref="EventSource.EventSource(Configuration)"/>
    /// <seealso cref="ConfigurationBuilder"/>
    public sealed class Configuration
    {
        #region Constants

        /// <summary>
        /// The default value for <see cref="ConfigurationBuilder.InitialRetryDelay(TimeSpan)"/>:
        /// one second.
        /// </summary>
        public static readonly TimeSpan DefaultInitialRetryDelay = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The logger name that will be used if you specified a logging implementation but did not
        /// provide a specific logger instance.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.LogAdapter(ILogAdapter)"/>
        public const string DefaultLoggerName = "EventSource";

        /// <summary>
        /// The default value for <see cref="ConfigurationBuilder.RetryDelayResetThreshold(TimeSpan)"/>:
        /// one minute.
        /// </summary>
        public static readonly TimeSpan DefaultRetryDelayResetThreshold = TimeSpan.FromMinutes(1);

        #endregion

        #region Public Properties

        /// <summary>
        /// The configured connection strategy.
        /// </summary>
        /// <seealso cref="Configuration.Builder(ConnectStrategy)"/>
        public ConnectStrategy ConnectStrategy { get; }

        /// <summary>
        /// The configured error strategy.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.ErrorStrategy(ErrorStrategy)"/>
        public ErrorStrategy ErrorStrategy { get; }

        /// <summary>
        /// A set of field names that are expected to appear before the data field in streaming mode.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.ExpectFields(string[])"/>
        public HashSet<string> ExpectFields { get; }

        /// <summary>
        /// The initial amount of time to wait before attempting to reconnect to the EventSource API.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.InitialRetryDelay(TimeSpan)"/>
        public TimeSpan InitialRetryDelay { get; }

        /// <summary>
        /// Gets the last event identifier.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.LastEventId(string)"/>
        public string LastEventId { get; }

        /// <summary>
        /// The logger to be used for all EventSource log output.
        /// </summary>
        /// <remarks>
        /// This is never null; if logging is not configured, it will be <c>LaunchDarkly.Logging.Logs.None</c>.
        /// </remarks>
        /// <seealso cref="ConfigurationBuilder.Logger(Logger)"/>
        public Logger Logger { get; }

        /// <summary>
        /// The configured retry delay strategy.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.RetryDelayStrategy(RetryDelayStrategy)"/>
        public RetryDelayStrategy RetryDelayStrategy { get; }

        /// <summary>
        /// The amount of time a connection must stay open before the EventSource resets its retry delay.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.RetryDelayResetThreshold(TimeSpan)"/>
        public TimeSpan RetryDelayResetThreshold { get; }

        /// <summary>
        /// True if EventSource should return not-fully-read events containing a stream for
        /// reading the data.
        /// </summary>
        /// <seealso cref="ConfigurationBuilder.StreamEventData(bool)"/>
        public bool StreamEventData { get; }

        #endregion

        #region Internal Constructor

        internal Configuration(ConfigurationBuilder builder)
        {
            var logger = builder._logger ??
                (builder._logAdapter is null ? null : builder._logAdapter.Logger(Configuration.DefaultLoggerName));

            ConnectStrategy = builder._connectStrategy;
            ErrorStrategy = builder._errorStrategy ?? ErrorStrategy.AlwaysThrow;
            ExpectFields = builder._expectFields;
            InitialRetryDelay = builder._initialRetryDelay;
            LastEventId = builder._lastEventId;
            Logger = logger ?? Logs.None.Logger("");
            RetryDelayStrategy = builder._retryDelayStrategy ?? RetryDelayStrategy.Default;
            RetryDelayResetThreshold = builder._retryDelayResetThreshold;
            StreamEventData = builder._streamEventData;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Provides a new <see cref="ConfigurationBuilder"/> for constructing a configuration.
        /// </summary>
        /// <remarks>
        /// Use this method if you do not need to configure any HTTP-related properties
        /// besides the URI. To specify a custom HTTP configuration instead, use
        /// <see cref="Builder(ConnectStrategy)"/> with <see cref="ConnectStrategy.Http(Uri)"/>
        /// and the <see cref="HttpConnectStrategy"/> configuration methods.
        /// </remarks>
        /// <param name="uri">the EventSource URI</param>
        /// <returns>a new builder instance</returns>
        /// <exception cref="ArgumentNullException">if the URI is null</exception>
        /// <seealso cref="Builder(ConnectStrategy)"/>
        public static ConfigurationBuilder Builder(Uri uri) =>
            new ConfigurationBuilder(ConnectStrategy.Http(uri));

        /// <summary>
        /// Provides a new <see cref="ConfigurationBuilder"/> for constructing a configuration,
        /// specifying how EventSource will connect to a stream.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="LaunchDarkly.EventSource.ConnectStrategy"/> will handle all details
        /// of how to obtain an input stream for the EventSource to consume. By default, this is
        /// <see cref="HttpConnectStrategy"/>, which makes HTTP requests. To customize the
        /// HTTP behavior, you can use methods of <see cref="HttpConnectStrategy"/>:
        /// </para>
        /// <example><code>
        ///     var config = Configuration.Builder(
        ///         ConnectStrategy.Http(streamUri)
        ///             .Header("name", "value")
        ///             .ReadTimeout(TimeSpan.FromMinutes(1))
        ///     );
        /// </code></example>
        /// <para>
        /// Or, if you want to consume an input stream from some other source, you can
        /// create your own subclass of <see cref="LaunchDarkly.EventSource.ConnectStrategy"/>.
        /// </para>
        /// </remarks>
        /// <param name="connectStrategy">the object that will manage the input stream;
        /// must not be null</param>
        /// <returns>a new builder instance</returns>
        /// <exception cref="ArgumentNullException">if the parameter is null</exception>
        /// <seealso cref="Builder(Uri)"/>
        public static ConfigurationBuilder Builder(ConnectStrategy connectStrategy) =>
            new ConfigurationBuilder(connectStrategy);

        #endregion
    }
}