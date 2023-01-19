using System.Collections.Concurrent;
using System.Threading.Tasks;
using LaunchDarkly.EventSource.Events;

using static LaunchDarkly.EventSource.TestHelpers;

namespace LaunchDarkly.EventSource.Background
{
	public class SimpleEventSink
	{
		private readonly BlockingCollection<IEvent> _received = new BlockingCollection<IEvent>();

        public IEvent Take() => RequireValue(_received);

        public class ClosedEvent : IEvent // this doesn't exist in the base API
        {
            private readonly ReadyState _state;

            public ClosedEvent(ReadyState state)
            {
                _state = state;
            }

            public override bool Equals(object obj) => obj is ClosedEvent ce &&
                ce._state == _state;

            public override int GetHashCode() => _state.GetHashCode();

            public override string ToString() => "ClosedEvent(" + _state + ")";
        }

        public void Listen(BackgroundEventSource bes)
        {
            bes.Opened += OnOpened;
            bes.MessageReceived += OnMessageReceived;
            bes.CommentReceived += OnCommentReceived;
            bes.Closed += OnClosed;
            bes.Error += OnError;
        }

        private Task Receive(IEvent e)
        {
            _received.Add(e);
            return Task.CompletedTask;
        }

        public Task OnOpened(object sender, StateChangedEventArgs args) =>
            Receive(new StartedEvent());

		public Task OnMessageReceived(object sender, MessageReceivedEventArgs args) =>
            Receive(args.Message);

        public Task OnCommentReceived(object sender, CommentReceivedEventArgs args) =>
            Receive(new CommentEvent(args.Comment));

        public Task OnClosed(object sender, StateChangedEventArgs args) =>
            Receive(new ClosedEvent(args.ReadyState));

        public Task OnError(object sender, ExceptionEventArgs args) =>
           Receive(new FaultEvent(args.Exception));
    }
}

