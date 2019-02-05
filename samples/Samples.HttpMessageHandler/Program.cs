using System;
using System.IO;
using System.Linq;
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
        private const string RequestValue = "PING";
        private const string ResponseValue = "PONG";
        private const string RequestHeader = "x-datadog-request";
        private const string ResponseHeader = "x-datadog-response";

        private static readonly Encoding Encoding = Encoding.UTF8;

        public static async Task Main()
        {
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(Url);
                listener.Start();

                // subscribe to http requests events
                Observable.FromAsync(listener.GetContextAsync)
                          .Subscribe(async context => await HandleHttpRequest(context));

                // send an http request
                await SendHttpRequest();

                listener.Stop();
            }
        }

        private static async Task SendHttpRequest()
        {
            var clientRequestContent = new StringContent(RequestValue, Encoding);

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add(RequestHeader, RequestValue);

                using (var responseMessage = await client.PostAsync(Url, clientRequestContent))
                {
                    var responseContent = await responseMessage.Content.ReadAsStringAsync();
                    var responseHeader = responseMessage.Headers.GetValues(ResponseHeader).Single();

                    Console.WriteLine($"[client] response header: {responseHeader}");
                    Console.WriteLine($"[client] response content: {responseContent}");
                }
            }
        }

        private static async Task HandleHttpRequest(HttpListenerContext context)
        {
            // read request content and header
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                string requestContent = await reader.ReadToEndAsync();
                string header = context.Request.Headers[RequestHeader];

                Console.WriteLine($"[server] request header: {header}");
                Console.WriteLine($"[server] request content: {requestContent}");
            }

            // add response header first, then write content
            context.Response.Headers[ResponseHeader] = ResponseValue;

            byte[] responseBytes = Encoding.GetBytes(ResponseValue);
            context.Response.ContentEncoding = Encoding;
            context.Response.ContentLength64 = responseBytes.Length;
            await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);

            // we must close output stream
            context.Response.OutputStream.Close();
        }
    }
}
