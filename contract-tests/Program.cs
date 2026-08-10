using System.Threading;
using LaunchDarkly.TestHelpers.HttpTest;

namespace TestService
{
    /// <summary>
    /// Desktop / server entry point for the SSE contract-tests service. The Webapp class
    /// (in TestService.cs) is shared with the Android target under contract-tests-android/,
    /// which has its own Activity-based entry point.
    /// </summary>
    public class Program
    {
        const int Port = 8000;

        public static void Main(string[] args)
        {
            var quitSignal = new EventWaitHandle(false, EventResetMode.AutoReset);

            var app = new Webapp(quitSignal);
            var server = HttpServer.Start(Port, app.Handler);
            server.Recorder.Enabled = false;

            System.Console.WriteLine("Listening on port {0}", Port);

            quitSignal.WaitOne();
            server.Dispose();
        }
    }
}
