using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Datadog.Core.Tools;
using Samples.Shared;

namespace Samples.WebClientDriver
{
    public static class Program
    {
        private const string RequestContent = "PING";
        private const string ResponseContent = "PONG";
        private static readonly Encoding Utf8 = Encoding.UTF8;
        private static Thread listenerThread;

        private static string Url;

        static void Main(string[] args)
        {
            bool tracingDisabled = args.Any(arg => arg.Equals("TracingDisabled", StringComparison.OrdinalIgnoreCase));
            Console.WriteLine($"TracingDisabled {tracingDisabled}");

            string port = args.FirstOrDefault(arg => arg.StartsWith("Port="))?.Split('=')[1] ?? "9000";
            Console.WriteLine($"Port {port}");

            using (var listener = StartHttpListenerWithPortResilience(port))
            {
                Console.WriteLine();
                Console.WriteLine($"Starting HTTP listener at {Url}");

                // send http requests using WebClient
                Console.WriteLine();
                Console.WriteLine("Sending request with WebClient.");
                WebClientHelpers.SendWebClientsRequest(tracingDisabled, Url, RequestContent);

                Console.WriteLine();
                Console.WriteLine("Stopping HTTP listener.");
                listener.Stop();
            }

            // Force process to end, otherwise the background listener thread lives forever in .NET Core.
            // Apparently listener.GetContext() doesn't throw an exception if listener.Stop() is called,
            // like it does in .NET Framework.
            Environment.Exit(0);
        }

        public static HttpListener StartHttpListenerWithPortResilience(string port, int retries = 5)
        {
            // try up to 5 consecutive ports before giving up
            while (true)
            {
                Url = $"http://localhost:{port}/Samples.WebClientDriver/";

                // seems like we can't reuse a listener if it fails to start,
                // so create a new listener each time we retry
                var listener = new HttpListener();
                listener.Prefixes.Add(Url);

                try
                {
                    listener.Start();

                    listenerThread = new Thread(HandleHttpRequests);
                    listenerThread.Start(listener);

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

        private static void HandleHttpRequests(object state)
        {
            var listener = (HttpListener)state;

            while (listener.IsListening)
            {
                try
                {
                    var context = listener.GetContext();

                    Console.WriteLine("[HttpListener] received request");

                    // read request content and headers
                    using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                    {
                        string requestContent = reader.ReadToEnd();
                        Console.WriteLine($"[HttpListener] request content: {requestContent}");

                        foreach (string headerName in context.Request.Headers)
                        {
                            string headerValue = context.Request.Headers[headerName];
                            Console.WriteLine($"[HttpListener] request header: {headerName}={headerValue}");
                        }
                    }

                    // write response content
                    byte[] responseBytes = Utf8.GetBytes(ResponseContent);
                    context.Response.ContentEncoding = Utf8;
                    context.Response.ContentLength64 = responseBytes.Length;
                    context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);

                    // we must close the response
                    context.Response.Close();
                }
                catch (HttpListenerException)
                {
                    // listener was stopped,
                    // ignore to let the loop end and the method return
                }
            }
        }
    }
}
