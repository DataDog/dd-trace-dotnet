using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Samples.HttpMessageHandler
{
    public static class Program
    {
        private const string RequestContent = "PING";
        private const string ResponseContent = "PONG";
        private static readonly Encoding Utf8 = Encoding.UTF8;

#pragma warning disable 1998
        public static async Task Main(string[] args)
#pragma warning restore 1998
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

                // send async http requests using HttpClient
                Console.WriteLine();
                Console.WriteLine("Sending async request with default HttpClient.");
                using (var client = new HttpClient())
                {
                    RequestHelpers.SendAsyncHttpClientRequests(client, tracingDisabled, url, RequestContent);
                }

                // send async http requests using HttpClient with CustomHandler
                Console.WriteLine();
                Console.WriteLine("Sending async request with HttpClient(CustomHandler).");
                using (var client = new HttpClient(new CustomHandler()))
                {
                    RequestHelpers.SendAsyncHttpClientRequests(client, tracingDisabled, url, RequestContent);
                }

#if !NET452
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // send async http requests using HttpClient with raw WinHttpHandler
                    Console.WriteLine();
                    Console.WriteLine("Sending async request with HttpClient(WinHttpHandler).");
                    using (var client = new HttpClient(new WinHttpHandler()))
                    {
                        RequestHelpers.SendAsyncHttpClientRequests(client, tracingDisabled, url, RequestContent);
                    }
                }
#endif

#if NETCOREAPP
                // send async http requests using HttpClient with raw SocketsHttpHandler
                Console.WriteLine();
                Console.WriteLine("Sending async request with HttpClient(SocketsHttpHandler).");
                using (var client = new HttpClient(new SocketsHttpHandler()))
                {
                    RequestHelpers.SendAsyncHttpClientRequests(client, tracingDisabled, url, RequestContent);
                }
#endif

#if NET5_0
                // send sync http requests using HttpClient
                Console.WriteLine();
                Console.WriteLine("Sending sync request with default HttpClient.");
                using (var client = new HttpClient())
                {
                    RequestHelpers.SendHttpClientRequests(client, tracingDisabled, url, RequestContent);
                }

                // send async http requests using HttpClient with CustomHandler
                Console.WriteLine();
                Console.WriteLine("Sending sync request with HttpClient(CustomHandler).");
                using (var client = new HttpClient(new CustomHandler()))
                {
                    RequestHelpers.SendHttpClientRequests(client, tracingDisabled, url, RequestContent);
                }

                // send sync http requests using HttpClient with raw SocketsHttpHandler
                Console.WriteLine();
                Console.WriteLine("Sending sync request with HttpClient(SocketsHttpHandler).");
                using (var client = new HttpClient(new SocketsHttpHandler()))
                {
                    RequestHelpers.SendHttpClientRequests(client, tracingDisabled, url, RequestContent);
                }

                // sync http requests using HttpClient are not supported with WinHttpHandler
#endif

#if NETCOREAPP2_1 || NETCOREAPP3_0 || NETCOREAPP3_1
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Console.WriteLine();
                    Console.WriteLine("Sending async request with internal WinHttpHandler.");
                    Type winHttpHandler = typeof(System.Net.Http.HttpMessageHandler).Assembly.GetTypes().FirstOrDefault(t => t.Name == "WinHttpHandler");
                    System.Net.Http.HttpMessageHandler handler = (System.Net.Http.HttpMessageHandler)Activator.CreateInstance(winHttpHandler);
                    using (var invoker = new HttpMessageInvoker(handler, false))
                    {
                        await RequestHelpers.SendHttpMessageInvokerRequestsAsync(invoker, tracingDisabled, url);
                    }
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("Sending async request with CurlHandler.");
                    Type curlHandlerType = typeof(System.Net.Http.HttpMessageHandler).Assembly.GetTypes().FirstOrDefault(t => t.Name == "CurlHandler");
                    System.Net.Http.HttpMessageHandler handler = (System.Net.Http.HttpMessageHandler)Activator.CreateInstance(curlHandlerType);
                    using (var invoker = new HttpMessageInvoker(handler, false))
                    {
                        await RequestHelpers.SendHttpMessageInvokerRequestsAsync(invoker, tracingDisabled, url);
                    }
                }
#endif

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
            if (context.Request.RawUrl == "/Samples.HttpMessageHandler/HttpErrorCode")
            {
                context.Response.StatusCode = 502;
            }

            context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);

            // we must close the response
            context.Response.Close();
        }
    }
}
