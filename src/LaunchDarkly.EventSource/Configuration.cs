using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// A class used 
    /// </summary>
    public class Configuration
    {

        #region Private Fields

        private readonly Uri _defaultUri = new Uri("https://stream.launchdarkly.com/flags");
        private Uri _uri;

        internal static readonly string Version = ((AssemblyInformationalVersionAttribute)typeof(EventSource)
                .GetTypeInfo()
                .Assembly
                .GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute)))
            .InformationalVersion;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets or sets the <see cref="System.Uri"/> used when connecting to an EventSource API.
        /// </summary>
        /// <value>
        /// The <see cref="System.Uri"/>.
        /// </value>
        public Uri Uri
        {
            get
            {
                if (_uri == null)
                    return _defaultUri;

                return _uri;
            }
            set { _uri = value; }
        }

        /// <summary>
        /// Gets or sets the connection time out value used when connecting to the EventSource API.
        /// </summary>
        /// <value>
        /// The connection time out.
        /// </value>
        public TimeSpan ConnectionTimeOut { get; set; }

        /// <summary>
        /// Gets or sets the duration to wait before attempting to reconnect to the EventSource API.
        /// </summary>
        /// <value>
        /// The duration of the retry delay.
        /// </value>
        public TimeSpan DelayRetryDuration { get; set; }

        /// <summary>
        /// Gets or sets the last event identifier.
        /// </summary>
        /// <remarks>
        /// Setting the LastEventId will add an HTTP request header named "Last-Event-ID" when connecting to the EventSource API
        /// </remarks>
        /// <value>
        /// The last event identifier.
        /// </value>
        public string LastEventId { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Microsoft.Extensions.Logging.ILogger"/> used internally in the <see cref="EventSource"/> class.
        /// </summary>
        /// <value>
        /// The ILogger to use for internal logging.
        /// </value>
        public ILogger Logger { get; set; }

        /// <summary>
        /// Gets or sets the request headers used when connecting to the EventSource API.
        /// </summary>
        /// <value>
        /// The request headers.
        /// </value>
        public Dictionary<string, string> RequestHeaders { get; set; }

        #endregion

        #region Public Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="Configuration"/> class.
        /// </summary>
        public Configuration()
        {
        }

        #endregion

    }
}
