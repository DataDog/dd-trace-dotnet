using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace;
using Datadog.Trace.TestHelpers;

namespace Samples.Telemetry
{
    internal static class Program
    {
        private static string Url;
        private static Task _listenerTask;
        private static HttpListener _listener;

        public static async Task Main(string[] args)
        {
            string port = args.FirstOrDefault(arg => arg.StartsWith("Port="))?.Split('=')[1] ?? "9000";
            Console.WriteLine($"Port {port}");

            using (_listener = StartHttpListenerWithPortResilience(port))
            {
                _listenerTask = Task.Run(HandleHttpRequests);
                Console.WriteLine();
                Console.WriteLine($"Starting HTTP listener at {Url}");

                // send async http requests using HttpClient
                Console.WriteLine();
                Console.WriteLine("Sending async request with default HttpClient.");
                using (var client = new HttpClient())
                {
                    using (Tracer.Instance.StartActive("GetAsync"))
                    {
                        await client.GetAsync(Url);
                        Console.WriteLine("Received response for client.GetAsync(String)");
                    }
                }
                Console.WriteLine();
                Console.WriteLine("Stopping HTTP listener.");
                _listener.Stop();
                _listenerTask.Wait();
            }

            Environment.Exit(0);
        }

        public static HttpListener StartHttpListenerWithPortResilience(string port, int retries = 5)
        {
            // try up to 5 consecutive ports before giving up
            while (true)
            {
                Url = $"http://localhost:{port}/Samples.HttpMessageHandler/";

                // seems like we can't reuse a listener if it fails to start,
                // so create a new listener each time we retry
                var listener = new HttpListener();
                listener.Prefixes.Add(Url);

                try
                {
                    listener.Start();
                    return listener;
                }
                catch (HttpListenerException) when (retries > 0)
                {
                    // only catch the exception if there are retries left
                    port = TcpPortProvider.GetOpenPort().ToString();
                    retries--;
                }

                // always close listener if exception is thrown,
                // whether it was caught or not
                listener.Close();
            }
        }

        private static async Task HandleHttpRequests()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();

                    Console.WriteLine("[HttpListener] received request");

                    // write response content
                    context.Response.StatusCode = 200;
                    context.Response.Close();
                }
                catch (HttpListenerException)
                {
                    // _listener was probably stopped,
                    // ignore to let the loop end and the method return
                }
                catch (ObjectDisposedException)
                {
                    // the response has been already disposed.
                }
                catch (InvalidOperationException)
                {
                    // this can occur when setting Response.ContentLength64, with the framework claiming that the response has already been submitted
                    // for now ignore, and we'll see if this introduces downstream issues
                }
                catch (Exception) when (!_listener.IsListening)
                {
                    // ignore to let the loop end and the method return
                }
            }
        }
    }
}
