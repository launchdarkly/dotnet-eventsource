using LaunchDarkly.EventSource;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;

namespace EventSource_ConsoleApp
{
    class Program
    {
        private static EventSource _evt;

        static void Main(string[] args)
        {

            Dictionary<string, string> headers =
                new Dictionary<string, string> {{"Authorization", "sdk-16c73e2d-5402-4b1b-840e-cb32a4c00ce2"}};

            var logFactory = new LoggerFactory();

            Log("Starting...");

            //var url = "http://live-test-scores.herokuapp.com/scores";
            var url = "https://stream.launchdarkly.com/flags";

            Configuration config = new Configuration(
                uri: new Uri(url),
                connectionTimeOut: Timeout.InfiniteTimeSpan,
                delayRetryDuration: TimeSpan.FromMilliseconds(1000),
                readTimeout: TimeSpan.FromMilliseconds(1000),
                requestHeaders: headers,
                logger: logFactory.CreateLogger<EventSource>()
            );

            logFactory.AddConsole(LogLevel.Trace);

            _evt = new EventSource(config);

            _evt.Opened += Evt_Opened;
            _evt.Error += Evt_Error;
            _evt.CommentReceived += Evt_CommentReceived;
            _evt.MessageReceived += Evt_MessageReceived;
            _evt.Closed += Evt_Closed;
            
            try
            {
                //evt.Start().Wait();
                _evt.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Current Time:{0}", DateTime.UtcNow);
                Console.WriteLine(ex);
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
            Log("EventSource Error Occurred. Details: {0}", e.Exception);
        }

        private static void Evt_Opened(object sender, StateChangedEventArgs e)
        {
            Log("EventSource Opened. Current State: {0}", e.ReadyState);
        }
    }
}
