using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.HttpMessageHandler
{
    public static class Program
    {
        private const string Url = "http://localhost:9000/Samples.HttpMessageHandler/";
        private const string RequestContent = "PING";
        private const string ResponseContent = "PONG";

        private static readonly Encoding Utf8 = Encoding.UTF8;

        public static void Main(string[] args)
        {
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(Url);
                listener.Start();

                if (args.Length == 0 || args[0].Equals("HttpClient", StringComparison.InvariantCultureIgnoreCase))
                {
                    // subscribe to http listener event
                    Observable.FromAsync(listener.GetContextAsync)
                              .Subscribe(async context => await HandleHttpRequest(context));

                    // send an http request using HttpClient
                    // SendHttpClientRequest().GetAwaiter().GetResult();
                }

                if (args.Length == 0 || args[0].Equals("WebClient", StringComparison.InvariantCultureIgnoreCase))
                {
                    // subscribe to http listener event
                    Observable.FromAsync(listener.GetContextAsync)
                              .Subscribe(async context => await HandleHttpRequest(context));

                    // send an http request using WebClient
                    SendWebClientRequest();
                }

                listener.Stop();
            }
        }

        private static async Task SendHttpClientRequest()
        {
            Console.WriteLine($"[HttpClient] sending request to {Url}");
            var clientRequestContent = new StringContent(RequestContent, Utf8);

            using (var client = new HttpClient())
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

        private static void SendWebClientRequest()
        {
            Console.WriteLine($"[WebClient] sending request to {Url}");

            using (var webClient = new WebClient())
            {
                webClient.Encoding = Utf8;

                var responseContent = webClient.DownloadString(Url);
                Console.WriteLine($"[WebClient] response content: {responseContent}");

                foreach (string headerName in webClient.ResponseHeaders)
                {
                    string headerValue = webClient.ResponseHeaders[headerName];
                    Console.WriteLine($"[WebClient] response header: {headerName}={headerValue}");
                }
            }
        }

        private static async Task HandleHttpRequest(HttpListenerContext context)
        {
            Console.WriteLine("[HttpListener] received request");

            // read request content and headers
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                string requestContent = await reader.ReadToEndAsync();
                Console.WriteLine($"[HttpListener] request content: {requestContent}");

                foreach (string headerName in context.Request.Headers)
                {
                    string headerValue = context.Request.Headers[headerName];
                    Console.WriteLine($"[HttpListener] request header: {headerName}={headerValue}");
                }
            }

            var delay = TimeSpan.FromSeconds(3);
            Console.WriteLine($"[HttpListener] Faking it for {delay}");
            await Task.Delay(delay);

            // write response content
            byte[] responseBytes = Utf8.GetBytes(ResponseContent);
            context.Response.ContentEncoding = Utf8;
            context.Response.ContentLength64 = responseBytes.Length;
            await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);

            // we must close the response
            context.Response.Close();
        }
    }
}
