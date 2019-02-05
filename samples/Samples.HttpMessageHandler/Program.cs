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
            var clientRequestContent = new StringContent(RequestContent, Encoding);

            using (var client = new HttpClient())
            using (var responseMessage = await client.PostAsync(Url, clientRequestContent))
            {
                // read response content and headers
                var responseContent = await responseMessage.Content.ReadAsStringAsync();

                Console.WriteLine($"[client] response content: {responseContent}");

                foreach (var responseHeader in responseMessage.Headers)
                {
                    var name = responseHeader.Key;
                    var values = string.Join(",", responseHeader.Value);
                    Console.WriteLine($"[client] response header: {name}={values}");
                }
            }
        }

        private static async Task HandleHttpRequest(HttpListenerContext context)
        {
            // read request content and headers
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                string requestContent = await reader.ReadToEndAsync();
                Console.WriteLine($"[server] request content: {requestContent}");

                foreach (string headerName in context.Request.Headers)
                {
                    string headerValue = context.Request.Headers[headerName];
                    Console.WriteLine($"[server] request header: {headerName}={headerValue}");
                }
            }

            // write response content
            byte[] responseBytes = Encoding.GetBytes(ResponseContent);
            context.Response.ContentEncoding = Encoding;
            context.Response.ContentLength64 = responseBytes.Length;
            await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);

            // we must close the response
            context.Response.Close();
        }
    }
}
