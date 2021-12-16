using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace;

namespace Samples.VersionConflict_2x
{
    class Program
    {
        static async Task Main()
        {
            using var server = WebServer.Start(out var url);
            
            server.RequestHandler = context =>
            {
                var traceId = context.Request.Headers[HttpHeaderNames.TraceId];
                var spanId = context.Request.Headers[HttpHeaderNames.ParentId];
                var samplingPriority = context.Request.Headers[HttpHeaderNames.SamplingPriority];

                Console.WriteLine($"{traceId}/{spanId}/{samplingPriority}");

                var payload = Encoding.UTF8.GetBytes("OK");

                context.Response.ContentEncoding = Encoding.UTF8;
                context.Response.ContentLength64 = payload.Length;
                context.Response.OutputStream.Write(payload, 0, payload.Length);
                context.Response.Close();
            };

            // Attempt to access the ActiveScope while the automatic ActiveScope is null
            var activeScope = Tracer.Instance.ActiveScope;
            if (activeScope is not null)
            {
                throw new InvalidOperationException("Tracer.Instance.ActiveScope should be null");
            }

            using (var scope = Tracer.Instance.StartActive("Manual"))
            {
                scope.Span.SetTag(Tags.SamplingPriority, "UserKeep");

                using (var client = new HttpClient())
                {
                    _ = await client.GetStringAsync(url + "/a");

                    scope.Span.SetTag(Tags.SamplingPriority, "UserReject");

                    _ = await client.GetStringAsync(url + "/b");
                }
            }
        }
    }
}
