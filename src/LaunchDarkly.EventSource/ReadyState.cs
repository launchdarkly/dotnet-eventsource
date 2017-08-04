using System;
using System.Collections.Generic;
using System.Text;

namespace LaunchDarkly.EventSource
{
    public enum ReadyState
    {
        Raw,
        Connecting,
        Open,
        Closed,
        Shutdown
    }
}
