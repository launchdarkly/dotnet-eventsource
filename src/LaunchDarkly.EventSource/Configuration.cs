using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;

namespace LaunchDarkly.EventSource
{
    public class Configuration
    {
        private readonly Uri _defaultUri = new Uri("https://stream.launchdarkly.com/flags");
        private Uri _uri;

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

        public TimeSpan ConnectionTimeOut { get; set; }

        public TimeSpan DelayRetryDuration { get; set; }

        public string LastEventId { get; set; }

        public ILoggerFactory LoggerFactory { get; set; }

        public Dictionary<string, string> RequestHeaders { get; set; }

        internal static readonly string Version = ((AssemblyInformationalVersionAttribute)typeof(EventSource)
                .GetTypeInfo()
                .Assembly
                .GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute)))
            .InformationalVersion;


        public Configuration()
        {
        }
    }
}
