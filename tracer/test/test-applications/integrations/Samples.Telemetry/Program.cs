using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Telemetry
{
    internal static class Program
    {
        private static ActivitySource _source;
        private const string ResponseContent = "PONG";
        private static readonly Encoding Utf8 = Encoding.UTF8;

        public static async Task Main(string[] args)
        {
            _source = new ActivitySource("Samples.Telemetry");

            string port = args.FirstOrDefault(arg => arg.StartsWith("Port="))?.Split('=')[1] ?? "9000";
            Console.WriteLine($"Port {port}");

            // Need to send an error log, but no easy way to do that on purpose.
            if (Environment.GetEnvironmentVariable("SEND_ERROR_LOG") == "1")
            {
                SendErrorLog();
            }

            using (var server = WebServer.Start(port, out var url))
            {
                server.RequestHandler = HandleHttpRequests;

                Console.WriteLine();
                Console.WriteLine($"Starting HTTP listener at {url}");

                // send async http requests using HttpClient
                Console.WriteLine();
                Console.WriteLine("Sending async request with default HttpClient.");
                using (var client = new HttpClient())
                {
                    using (_source.StartActivity("GetAsync"))
                    {
                        await client.GetAsync(url);
                        Console.WriteLine("Received response for client.GetAsync(String)");
                    }
                }

                Console.WriteLine();
                Console.WriteLine("Stopping HTTP listener.");
            }

            // Force process to end, otherwise the background listener thread lives forever in .NET Core.
            // Apparently listener.GetContext() doesn't throw an exception if listener.Stop() is called,
            // like it does in .NET Framework.
            Environment.Exit(0);
        }

        private static void HandleHttpRequests(HttpListenerContext context)
        {
            Activity activity = null;
            try
            {
                activity = new Activity("HttpListener.ReceivedRequest");
                activity.Start();

                Console.WriteLine("[HttpListener] received request");

                // read request content and headers
                using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                {
                    string requestContent = reader.ReadToEnd();
                    Console.WriteLine($"[HttpListener] request content: {requestContent}");
                }

                // write response content
                activity.SetTag("content", ResponseContent);
                byte[] responseBytes = Utf8.GetBytes(ResponseContent);
                context.Response.ContentEncoding = Utf8;
                context.Response.ContentLength64 = responseBytes.Length;

                context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);

                // we must close the response
                context.Response.Close();
            }
            finally
            {
                activity?.Dispose();
            }
        }

        private static void SendErrorLog([CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
        {
            if (!SampleHelpers.IsProfilerAttached())
            {
                throw new Exception("Can't send error log unless profiler is attached");
            }

            // grab the log field from TracerSettings, as it's an easy way to get an instance
            var settingsType = Type.GetType("Datadog.Trace.Configuration.TracerSettings, Datadog.Trace")!;
            var logField = settingsType.GetField("Log", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            var logger = logField.GetValue(null);

            var loggerType = Type.GetType("Datadog.Trace.Logging.DatadogSerilogLogger, Datadog.Trace")!;
            var errorMethod = loggerType.GetMethod("Error", [typeof(string)])!;

            errorMethod.Invoke(logger, ["Sending an error log using hacky reflection"]);
        }
    }
}
