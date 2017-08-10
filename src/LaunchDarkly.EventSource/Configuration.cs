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
        //"https://stream.launchdarkly.com/flags"

        public Uri Uri { get; set; }

        public TimeSpan ConnectionTimeOut { get; set; }

        public TimeSpan DelayRetryDuration { get; set; }

        public string LastEventId { get; set; }

        public ILoggerFactory LoggerFactory { get; set; }

        public HttpRequestHeaders HttpRequestHeaders { get; set; }

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
