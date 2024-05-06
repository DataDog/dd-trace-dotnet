using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Samples.WebRequest
{
    public static class Program
    {
        private const string TraceId = "x-datadog-trace-id";
        private const string ParentId = "x-datadog-parent-id";
        private const string SamplingPriority = "x-datadog-sampling-priority";

        private const string RequestContent = "PING";
        private const string ResponseContent = "PONG";
        private static readonly Encoding Utf8 = Encoding.UTF8;

        private static readonly string[] ExpectedHeaders = { TraceId, ParentId, SamplingPriority };

        private static bool _tracingDisabled;
        private static bool _ignoreAsync;

        public static async Task Main(string[] args)
        {
            _tracingDisabled = args.Any(arg => arg.Equals("TracingDisabled", StringComparison.OrdinalIgnoreCase));
            _ignoreAsync = args.Any(arg => arg.Equals("IgnoreAsync", StringComparison.OrdinalIgnoreCase));
            Console.WriteLine($"TracingDisabled {_tracingDisabled}, IgnoreAsync: {_ignoreAsync}");

            string port = args.FirstOrDefault(arg => arg.StartsWith("Port="))?.Split('=')[1] ?? "9000";
            Console.WriteLine($"Port {port}");

            using (var server = WebServer.Start(port, out string url))
            {
                server.RequestHandler = HandleHttpRequests;

                Console.WriteLine();
                Console.WriteLine($"Starting HTTP listener at {url}");

                // send http requests using WebClient
                Console.WriteLine();
                Console.WriteLine("Sending requests with WebClient.");
                await RequestHelpers.SendWebClientRequests(_tracingDisabled, url, RequestContent);

                // send http requests using WebRequest
                Console.WriteLine();
                Console.WriteLine("Sending requests with WebRequest.");
                await RequestHelpers.SendWebRequestRequests(_tracingDisabled, url, RequestContent);

                Console.WriteLine();
                Console.WriteLine("Stopping HTTP listener.");
            }

            await SampleHelpers.ForceTracerFlushAsync();
        }

        private static void HandleHttpRequests(HttpListenerContext context)
        {
            Console.WriteLine("[HttpListener] received request");

            // read request content and headers.
            // output the headers to the console first _before_ asserting their presence.
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

            // Check Datadog headers
            if (!_ignoreAsync || context.Request.Url.Query.IndexOf("Async", StringComparison.OrdinalIgnoreCase) == -1)
            {
                foreach (var header in ExpectedHeaders)
                {
                    bool headerMissing = context.Request.Headers[header] == null;

                    if (_tracingDisabled)
                    {
                        if (!headerMissing)
                        {
                            Console.Error.WriteLine($"Found header {header} for request {context.Request.Url}");
                            Environment.Exit(-1);
                        }
                    }
                    else
                    {
                        if (headerMissing)
                        {
                            Console.Error.WriteLine($"Missing header {header} for request {context.Request.Url}");
                            Environment.Exit(-1);
                        }
                    }
                }
            }

            // write response content
            byte[] responseBytes = Utf8.GetBytes(ResponseContent);
            context.Response.ContentEncoding = Utf8;
            context.Response.ContentLength64 = responseBytes.Length;
            if (context.Request.RawUrl.Contains("NotFound"))
            {
                context.Response.StatusCode = 404;
            }
            else if (context.Request.RawUrl.Contains("Teapot"))
            {
                context.Response.StatusCode = 418;
            }

            context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);

            // we must close the response
            context.Response.Close();
        }
    }
}
