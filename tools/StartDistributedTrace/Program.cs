using System;
using System.Globalization;
using System.Net.Http;
using Datadog.Trace;
using Datadog.Trace.Headers;

namespace StartDistributedTrace
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine($"Usage: {nameof(StartDistributedTrace)} <url>");
                return;
            }

            string url = args[0];
            Console.WriteLine($"[HttpClient] sending request to {url}");

            using (var scope = Tracer.Instance.StartActive("http.request"))
            using (var client = new HttpClient())
            {
                // Set basic span information for HTTP operations
                var span = scope.Span;
                span.Type = SpanTypes.Http;
                span.SetTag(Tags.HttpMethod, "GET");
                span.SetTag(Tags.HttpUrl, url);

                // Set distributed tracing headers
                client.DefaultRequestHeaders.Add(HttpHeaderNames.TraceId, span.TraceId.ToString(CultureInfo.InvariantCulture));
                client.DefaultRequestHeaders.Add(HttpHeaderNames.ParentId, span.SpanId.ToString(CultureInfo.InvariantCulture));

                // Send HTTP request
                using (var responseMessage = client.GetAsync(url).Result)
                {
                    // read response content and headers
                    var responseContent = responseMessage.Content.ReadAsStringAsync().Result;
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
    }
}
