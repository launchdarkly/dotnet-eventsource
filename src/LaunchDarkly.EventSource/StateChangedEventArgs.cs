using System;

namespace LaunchDarkly.EventSource
{
    public class StateChangedEventArgs : EventArgs
    {
        public ReadyState State { get; private set; }
        public StateChangedEventArgs(ReadyState state)
        {
            State = state;
        }
    }
}