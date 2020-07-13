using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace;

namespace StartDistributedTrace
{
    public class Program
    {
        public static async Task Main(string[] args)
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
                client.DefaultRequestHeaders.Add("upstream-service", nameof(StartDistributedTrace));

                // Send HTTP request
                using (var responseMessage = await client.GetAsync(url))
                {
                    // read response content and headers
                    var responseContent = await responseMessage.Content.ReadAsStringAsync();

                    foreach (var header in responseMessage.Headers)
                    {
                        var name = header.Key;
                        var values = string.Join(",", header.Value);
                        Console.WriteLine($"[HttpClient] response header: {name}={values}");
                    }

                    Console.WriteLine($"[HttpClient] response content: {responseContent}");
                }
            }
        }
    }
}
