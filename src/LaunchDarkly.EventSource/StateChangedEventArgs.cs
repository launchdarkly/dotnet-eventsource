using System;

namespace LaunchDarkly.EventSource
{
    public class StateChangedEventArgs : EventArgs
    {
        public ReadyState ReadyState { get; private set; }

        public StateChangedEventArgs(ReadyState readyState)
        {
            ReadyState = readyState;
        }
    }
}