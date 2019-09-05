using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace;

namespace Samples.HttpMessageHandler
{
    public static class Program
    {
        private const string RequestContent = "PING";
        private const string ResponseContent = "PONG";
        private static readonly Encoding Utf8 = Encoding.UTF8;

        private static string Url;

        public static void Main(string[] args)
        {
            bool tracingDisabled = args.Any(arg => arg.Equals("TracingDisabled", StringComparison.OrdinalIgnoreCase));
            Console.WriteLine($"TracingDisabled {tracingDisabled}");

            bool useHttpClient = args.Any(arg => arg.Equals("HttpClient", StringComparison.OrdinalIgnoreCase));
            Console.WriteLine($"HttpClient {useHttpClient}");

            bool useWebClient = args.Any(arg => arg.Equals("WebClient", StringComparison.OrdinalIgnoreCase));
            Console.WriteLine($"WebClient {useWebClient}");

            string port = args.FirstOrDefault(arg => arg.StartsWith("Port="))?.Split('=')[1] ?? "9000";
            Console.WriteLine($"Port {port}");

            Url = $"http://localhost:{port}/Samples.HttpMessageHandler/";

            Console.WriteLine();
            Console.WriteLine($"Starting HTTP listener at {Url}");

            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(Url);
                listener.Start();

                // handle http requests in a background thread
                var listenerThread = new Thread(HandleHttpRequests);
                listenerThread.Start(listener);

                if (args.Length == 0 || args.Any(arg => arg.Equals("HttpClient", StringComparison.OrdinalIgnoreCase)))
                {
                    // send an http request using HttpClient
                    Console.WriteLine();
                    Console.WriteLine("Sending request with HttpClient.");
                    SendHttpClientRequestAsync(tracingDisabled).GetAwaiter().GetResult();
                }

                if (args.Length == 0 || args.Any(arg => arg.Equals("WebClient", StringComparison.OrdinalIgnoreCase)))
                {
                    // send an http request using WebClient
                    Console.WriteLine();
                    Console.WriteLine("Sending request with WebClient.");
                    SendWebClientRequest(tracingDisabled);
                }

                Console.WriteLine();
                Console.WriteLine("Stopping HTTP listener.");
                listener.Stop();
            }

            // Force process to end, otherwise the background listener thread lives forever in .NET Core.
            // Apparently listener.GetContext() doesn't throw an exception if listener.Stop() is called,
            // like it does in .NET Framework.
            Environment.Exit(0);
        }

        private static async Task SendHttpClientRequestAsync(bool tracingDisabled)
        {
            var ins = Tracer.Instance;
            Console.WriteLine($"[HttpClient] sending request to {Url}");
            var clientRequestContent = new StringContent(RequestContent, Utf8);

            using (var client = new HttpClient())
            {
                if (tracingDisabled)
                {
                    client.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");
                }

                using (var responseMessage = await client.PostAsync(Url, clientRequestContent))
                {
                    // read response content and headers
                    var responseContent = await responseMessage.Content.ReadAsStringAsync();
                    Console.WriteLine($"[HttpClient] response content: {responseContent}");

                    foreach (var header in responseMessage.Headers)
                    {
                        var name = header.Key;
                        var values = string.Join(",", header.Value);
                        Console.WriteLine($"[HttpClient] response header: {name}={values}");
                    }
                }
            }
        }

        private static void SendWebClientRequest(bool tracingDisabled)
        {
            Console.WriteLine($"[WebClient] sending request to {Url}");

            using (var webClient = new WebClient())
            {
                webClient.Encoding = Utf8;

                if (tracingDisabled)
                {
                    webClient.Headers.Add(HttpHeaderNames.TracingEnabled, "false");
                }

                var responseContent = webClient.DownloadString(Url);
                Console.WriteLine($"[WebClient] response content: {responseContent}");

                foreach (string headerName in webClient.ResponseHeaders)
                {
                    string headerValue = webClient.ResponseHeaders[headerName];
                    Console.WriteLine($"[WebClient] response header: {headerName}={headerValue}");
                }
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
