using System;

namespace LaunchDarkly.EventSource
{
    public class MessageReceivedEventArgs : EventArgs
    {
        public MessageEvent Message { get; private set; }

        public string EventName { get; private set; }

        public MessageReceivedEventArgs(MessageEvent message, string eventName)
        {
            Message = message;
            EventName = eventName;
        }
    }
}