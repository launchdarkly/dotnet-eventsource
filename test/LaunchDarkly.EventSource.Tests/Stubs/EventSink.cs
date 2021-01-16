using System;
using System.Collections.Concurrent;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.EventSource.Tests
{
    public class EventSink
    {
        public bool? ExpectUtf8Data { get; set; }
        public Action<string> Output { get; set; }

        private readonly BlockingCollection<Action> _actions = new BlockingCollection<Action>();

        public class Action
        {
            public string Kind { get; set; }
            public string Comment { get; set; }
            public string EventName { get; set; }
            public MessageEvent Message { get; set; }
            public ReadyState ReadyState { get; set; }
            public Exception Exception { get; set; }

            public override bool Equals(object o) =>
                o is Action a &&
                Kind == a.Kind &&
                Comment == a.Comment &&
                EventName == a.EventName &&
                object.Equals(Message, a.Message) &&
                ReadyState == a.ReadyState &&
                object.Equals(Exception, a.Exception);

            public override string ToString()
            {
                switch (Kind)
                {
                    case "Opened":
                    case "Closed":
                        return Kind + "(" + ReadyState + ")";
                    case "CommentReceived":
                        return Kind + "(" + Comment + ")";
                    case "MessageReceived":
                        return Kind + "(" + EventName + "," + Message.Data + ")";
                    case "Error":
                        return Kind + "(" + Exception + ")";
                    default:
                        return Kind;
                }
            }
        }

        public EventSink(EventSource es)
        {
            es.Opened += OnOpened;
            es.Closed += OnClosed;
            es.CommentReceived += OnCommentReceived;
            es.MessageReceived += OnMessageReceived;
            es.Error += OnError;
        }

        public static Action OpenedAction(ReadyState state) =>
            new Action { Kind = "Opened", ReadyState = state };

        public static Action ClosedAction(ReadyState state) =>
            new Action { Kind = "Closed", ReadyState = state };

        public static Action CommentReceivedAction(string comment) =>
            new Action { Kind = "CommentReceived", Comment = comment };

        public static Action MessageReceivedAction(string eventName, MessageEvent message) =>
            new Action { Kind = "MessageReceived", EventName = eventName, Message = message };

        public static Action ErrorAction(Exception e) =>
            new Action { Kind = "Error", Exception = e };

        public void OnOpened(object sender, StateChangedEventArgs args) =>
            Add(OpenedAction(args.ReadyState));

        public void OnClosed(object sender, StateChangedEventArgs args) =>
            Add(ClosedAction(args.ReadyState));

        public void OnCommentReceived(object sender, CommentReceivedEventArgs args) =>
            Add(CommentReceivedAction(args.Comment));

        public void OnMessageReceived(object sender, MessageReceivedEventArgs args) =>
            Add(MessageReceivedAction(args.EventName, args.Message));

        public void OnError(object sender, ExceptionEventArgs args) =>
            Add(ErrorAction(args.Exception));

        private void Add(Action a)
        {
            Output?.Invoke("handler received: " + a);
            _actions.Add(a);
        }

        public Action ExpectAction()
        {
            Assert.True(_actions.TryTake(out var ret, TimeSpan.FromSeconds(5)));
            return ret;
        }

        public void ExpectActions(params Action[] expectedActions)
        {
            int i = 0;
            foreach (var a in expectedActions)
            {
                Assert.True(_actions.TryTake(out var actual, TimeSpan.FromSeconds(5)),
                    "timed out waiting for action " + i + " (" + a + ")");
                if (!a.Equals(actual))
                {
                    Assert.True(false, "action " + i + " should have been " + a + ", was " + actual);
                    a.Equals(actual);
                }
                if (actual.Kind == "MessageReceived" && ExpectUtf8Data.HasValue)
                {
                    if (actual.Message.IsDataUtf8Bytes != ExpectUtf8Data.Value)
                    {
                        Assert.True(false, "action " + i + "(" + actual + ") - data should have been read as "
                            + (ExpectUtf8Data.Value ? "UTF8 bytes" : "string"));
                    }
                    Assert.True(actual.Message.DataUtf8Bytes.Equals(a.Message.DataUtf8Bytes));
                }
                i++;
            }
        }
    }
}
