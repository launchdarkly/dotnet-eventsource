using System;
using System.Collections.Generic;
using System.Text;

namespace LaunchDarkly.EventSource
{
    internal static class Constants
    {
        internal static string AcceptHttpHeader = "Accept";

        internal static string LastEventIdHttpHeader = "Last-Event-ID";

        internal static string ContentType = "text/event-stream";

        internal static string RetryField = "retry";

        internal static string IdField = "id";

        internal static string EventField = "event";

        internal static string DataField = "data";

        internal static string MessageField = "message";
    }
}
