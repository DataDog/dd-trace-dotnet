using Datadog.Trace;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpListenerExample
{
    public static class RequestHelpers
    {
        private static readonly Encoding Utf8 = Encoding.UTF8;

        public static async Task SendHttpClientRequestsAsync(HttpClient client, bool tracingDisabled, string url, string requestContent)
        {
            // Insert a call to the Tracer.Instance to include an AssemblyRef to Datadog.Trace assembly in the final executable
            Console.WriteLine($"[HttpClient] sending requests to {url}");

            if (tracingDisabled)
            {
                client.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");
            }

            using (Tracer.Instance.StartActive("HttpClientRequestAsync"))
            {
                await client.DeleteAsync(url);
                Console.WriteLine("Received response for client.DeleteAsync(String)");

                await client.GetAsync(url);
                Console.WriteLine("Received response for client.GetAsync(String)");

                await client.PostAsync(url, new StringContent(requestContent, Utf8));
                Console.WriteLine("Received response for client.PostAsync(String, HttpContent)");

                await client.PutAsync(url, new StringContent(requestContent, Utf8));
                Console.WriteLine("Received response for client.PutAsync(String, HttpContent)");

                await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));
                Console.WriteLine("Received response for client.SendAsync(HttpRequestMessage)");

                await client.GetAsync($"{url}HttpErrorCode");
                Console.WriteLine("Received response for client.GetAsync Error Span");
            }
        }

        public static async Task SendHttpMessageInvokerRequestsAsync(HttpMessageInvoker invoker, bool tracingDisabled, string url)
        {
            Console.WriteLine($"[HttpMessageInvoker] sending requests to {url}");

            var httpRequest = new HttpRequestMessage();

            if (tracingDisabled)
            {
                httpRequest.Headers.Add(HttpHeaderNames.TracingEnabled, "false");
            }

            httpRequest.Method = HttpMethod.Get;
            httpRequest.RequestUri = new Uri(url);

            Console.WriteLine("Received response for HttpMessageInvoker.SendAsync");
            await invoker.SendAsync(httpRequest, CancellationToken.None);
        }
    }
}
