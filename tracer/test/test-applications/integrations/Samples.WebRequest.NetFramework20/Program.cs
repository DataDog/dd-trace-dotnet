using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Datadog.Trace;

namespace Samples.WebRequest.NetFramework20
{
    public static class Program
    {
        private const string RequestContent = "PING";
        private const string ResponseContent = "PONG";
        private static readonly Encoding Utf8 = Encoding.UTF8;

        public static void Main(string[] args)
        {
            bool tracingDisabled = args.Any(arg => arg.Equals("TracingDisabled", StringComparison.OrdinalIgnoreCase));
            Console.WriteLine($"TracingDisabled {tracingDisabled}");

            string port = args.FirstOrDefault(arg => arg.StartsWith("Port="))?.Split('=')[1] ?? "9000";
            Console.WriteLine($"Port {port}");

            using (var server = WebServer.Start(port, out var url))
            {
                server.RequestHandler = HandleHttpRequests;

                Console.WriteLine();
                Console.WriteLine($"Starting HTTP listener at {url}");

                // send http requests using WebClient
                Console.WriteLine();
                Console.WriteLine("Sending request with WebClient.");

                using (Tracer.Instance.StartActive("RequestHelpers.SendWebClientRequests"))
                {
                    RequestHelpers.SendWebClientRequests(tracingDisabled, url, RequestContent);
                }

                Console.WriteLine("Sending request with WebRequest.");

                using (Tracer.Instance.StartActive("RequestHelpers.SendWebRequestRequests"))
                {
                    RequestHelpers.SendWebRequestRequests(tracingDisabled, url, RequestContent);
                }

                Console.WriteLine();
                Console.WriteLine("Stopping HTTP listener.");
            }
        }

        private static void HandleHttpRequests(HttpListenerContext context)
        {
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
    }
}
