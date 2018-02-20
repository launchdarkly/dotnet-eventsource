using System;
using System.Collections.Generic;
using System.Threading;
using LaunchDarkly.EventSource;

namespace EventSource_ConsoleApp_DotNetCore
{
    class Program
    {
        private static EventSource _evt;

        static void Main(string[] args)
        {

            Dictionary<string, string> headers =
                new Dictionary<string, string> {{ "Authorization", "<Insert Auth Key>" }};

            Log("Starting...");

            var url = "<Insert API URL Here>";

            var connnectionTimeout = TimeSpan.FromMilliseconds(Timeout.Infinite); //Use Timeout.Infinite if you want a connection that does not timeout.

            Configuration config = new Configuration(
                uri: new Uri(url),
                connectionTimeOut: connnectionTimeout,
                delayRetryDuration: TimeSpan.FromMilliseconds(1000),
                readTimeout: TimeSpan.FromMinutes(4),
                requestHeaders: headers
            );

            _evt = new EventSource(config);

            _evt.Opened += Evt_Opened;
            _evt.Error += Evt_Error;
            _evt.CommentReceived += Evt_CommentReceived;
            _evt.MessageReceived += Evt_MessageReceived;
            _evt.Closed += Evt_Closed;

            try
            {
                _evt.StartAsync();
            }
            catch (Exception ex)
            {
                Log("General Exception: {0}", ex);
            }

            Console.ReadKey();
        }

        private static void Log(string format, params object[] args)
        {
            Console.WriteLine("{0}: {1}", DateTime.Now, string.Format(format, args));
        }

        private static void Evt_Closed(object sender, StateChangedEventArgs e)
        {
            Log("EventSource Closed. Current State {0}", e.ReadyState);
        }

        private static void Evt_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            Log("EventSource Message Received. Event Name: {0}", e.EventName);
            Log("EventSource Message Properties: {0}\tLast Event Id: {1}{0}\tOrigin: {2}{0}\tData: {3}",
                Environment.NewLine, e.Message.LastEventId, e.Message.Origin, e.Message.Data);
        }

        private static void Evt_CommentReceived(object sender, CommentReceivedEventArgs e)
        {
            Log("EventSource Comment Received: {0}", e.Comment);
        }

        private static void Evt_Error(object sender, ExceptionEventArgs e)
        {
            Log("EventSource Error Occurred. Details: {0}", e.Exception.Message);
        }

        private static void Evt_Opened(object sender, StateChangedEventArgs e)
        {
            Log("EventSource Opened. Current State: {0}", e.ReadyState);
        }
    }
}