using System;

namespace LaunchDarkly.EventSource
{
    public class ReadTimeoutException : Exception
    {
        public override string Message => Resources.EventSourceService_Read_Timeout;
    }
}
