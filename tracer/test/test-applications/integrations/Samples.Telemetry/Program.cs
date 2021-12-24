using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace;

namespace Samples.Telemetry
{
    internal static class Program
    {
        private const string ResponseContent = "PONG";
        private static readonly Encoding Utf8 = Encoding.UTF8;

        public static async Task Main(string[] args)
        {

            string port = args.FirstOrDefault(arg => arg.StartsWith("Port="))?.Split('=')[1] ?? "9000";
            Console.WriteLine($"Port {port}");

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
                    using (Tracer.Instance.StartActive("GetAsync"))
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
            Console.WriteLine("[HttpListener] received request");

            // read request content and headers
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                string requestContent = reader.ReadToEnd();
                Console.WriteLine($"[HttpListener] request content: {requestContent}");
            }

            // write response content
            byte[] responseBytes = Utf8.GetBytes(ResponseContent);
            context.Response.ContentEncoding = Utf8;
            context.Response.ContentLength64 = responseBytes.Length;

            context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);

            // we must close the response
            context.Response.Close();
        }
    }
}
