using System;
using System.Collections.Generic;
using System.Text;

namespace LaunchDarkly.EventSource
{
    public class CommentReceivedEventArgs : EventArgs
    {
        public string Comment { get; private set; }

        public CommentReceivedEventArgs(string comment)
        {
            Comment = comment;
        }
    }
}
