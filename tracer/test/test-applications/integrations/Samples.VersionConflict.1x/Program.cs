using System;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace;

namespace Samples.VersionConflict_1x
{
    class Program
    {
        static async Task Main()
        {
            using (WebServer.Start(out var url))
            {
                // Attempt to access the ActiveScope while the automatic ActiveScope is null
                var activeScope = Tracer.Instance.ActiveScope;
                if (activeScope is null)
                {
                    Console.WriteLine("As expected, initial Tracer.Instance.ActiveScope == null");
                }

                using (var scope = Tracer.Instance.StartActive("Manual"))
                {
                    scope.Span.SetTag(Tags.SamplingPriority, "UserKeep");

                    using (var client = new HttpClient())
                    {
                        _ = await client.GetStringAsync(url);

                        scope.Span.SetTag(Tags.SamplingPriority, "UserReject");
                    }
                }
            }
        }
    }
}
