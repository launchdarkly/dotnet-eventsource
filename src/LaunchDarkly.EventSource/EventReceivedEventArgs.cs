namespace LaunchDarkly.EventSource
{
    public class EventReceivedEventArgs
    {
        public MessageEvent Message { get; private set; }

        public EventReceivedEventArgs(MessageEvent message)
        {
            Message = message;
        }
    }
}