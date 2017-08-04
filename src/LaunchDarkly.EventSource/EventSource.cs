using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace LaunchDarkly.EventSource
{
    public class EventSource
    {
        private HttpClient _client;

        public event EventHandler<StateChangedEventArgs> StateChanged;
        public event EventHandler<EventReceivedEventArgs> EventReceived;

        public EventSource(HttpClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        protected void OnEventReceived(MessageEvent message)
        {
            if (EventReceived != null)
            {
                EventReceived(this, new EventReceivedEventArgs(message));
            }
        }

        protected void OnStateChanged(ReadyState newState)
        {
            if (StateChanged != null)
            {
                StateChanged(this, new StateChangedEventArgs(newState));
            }
        }
    }
}
