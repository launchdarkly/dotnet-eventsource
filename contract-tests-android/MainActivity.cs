using System;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.OS;
using LaunchDarkly.TestHelpers.HttpTest;
using TestService;

namespace ContractTestService.Android
{
    /// <summary>
    /// Android entry point for the SSE contract-tests service. Starts the same Webapp used by
    /// the desktop test service (contract-tests/) on a background thread when the activity
    /// launches. The HTTP endpoints, capabilities list, and routing logic are all defined by
    /// the shared Webapp class in TestService.cs -- this Activity only takes care of process
    /// hosting on Android.
    ///
    /// The purpose of exercising the contract tests through this Android app is to route SSE
    /// stream reads through AndroidMessageHandler + BufferedStream + the
    /// Java InputStream chain, which is the platform-specific path where SDK-2755's stall bug
    /// manifests. Running the harness against this app on an emulator is how we protect that
    /// fix from regression.
    /// </summary>
    [Activity(Label = "ContractTestService", MainLauncher = true)]
    public class MainActivity : Activity
    {
        const int Port = 8000;
        const string LogTag = "ContractTestService";

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Task.Run(RunHttpServer);
        }

        private void RunHttpServer()
        {
            try
            {
                var quitSignal = new EventWaitHandle(false, EventResetMode.AutoReset);
                var app = new Webapp(quitSignal);
                var server = HttpServer.Start(Port, app.Handler);
                server.Recorder.Enabled = false;
                global::Android.Util.Log.Info(LogTag, $"Listening on port {Port}");
                quitSignal.WaitOne();
                server.Dispose();
            }
            catch (Exception e)
            {
                global::Android.Util.Log.Error(LogTag, $"HTTP server failed: {e}");
            }
        }
    }
}
