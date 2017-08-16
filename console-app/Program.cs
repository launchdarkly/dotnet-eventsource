using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.EventSource;
using Microsoft.Extensions.Logging;

namespace EventSource_ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            HttpClient client = new HttpClient();
            
            Dictionary<string, string> headers = new Dictionary<string, string>();

            headers.Add("User-Agent", "DotNetClient/0.0.1");
            headers.Add("Authorization", "sdk-16c73e2d-5402-4b1b-840e-cb32a4c00ce2");

            var logFactory = new LoggerFactory();

            Console.WriteLine("Current Time:{0}", DateTime.UtcNow);

            Configuration config = new Configuration(
                uri: new Uri("https://stream.launchdarkly.com/flags"),
                connectionTimeOut: Timeout.InfiniteTimeSpan,
                delayRetryDuration: TimeSpan.FromMilliseconds(3000),
                requestHeaders: headers,
                logger: logFactory.CreateLogger<EventSource>()
            );

            logFactory.AddConsole(LogLevel.Trace);

            EventSource evt = new EventSource(config);

            evt.Opened += Evt_Opened;
            evt.Error += Evt_Error;
            evt.CommentReceived += Evt_CommentReceived;
            evt.MessageReceived += Evt_MessageReceived;
            evt.Closed += Evt_Closed;


            try
            {
                evt.Start().Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Current Time:{0}", DateTime.UtcNow);
                Console.WriteLine(ex);
            }

            Console.ReadKey();
        }

        private static void Evt_Closed(object sender, StateChangedEventArgs e)
        {
            Console.WriteLine("EventSource Closed. Current State {0}", e.ReadyState);
        }

        private static void Evt_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            Console.WriteLine("EventSource Message Received. Event Name: {0}", e.EventName);
            Console.WriteLine("EventSource Message Properties: {0}\tLast Event Id: {1}{0}\tOrigin: {2}{0}\tData: {3}",
                Environment.NewLine, e.Message.LastEventId, e.Message.Origin, e.Message.Data);
        }

        private static void Evt_CommentReceived(object sender, CommentReceivedEventArgs e)
        {
            Console.WriteLine("EventSource Comment Received: {0}", e.Comment);
        }

        private static void Evt_Error(object sender, ExceptionEventArgs e)
        {
            Console.WriteLine("EventSource Error Occurred. Details: {0}", e.Exception);
        }

        private static void Evt_Opened(object sender, StateChangedEventArgs e)
        {
            Console.WriteLine("EventSource Opened. Current State: {0}", e.ReadyState);
        }
    }
}
