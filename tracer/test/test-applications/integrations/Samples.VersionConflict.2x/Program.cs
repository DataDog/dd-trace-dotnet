using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace;

namespace Samples.VersionConflict_2x
{
    class Program
    {
        static async Task Main()
        {
            Debugger.Launch();

            using (WebServer.Start(out var url))
            {
                using (var scope = Tracer.Instance.StartActive("Manual"))
                {
                    scope.Span.SetTag(Tags.SamplingPriority, "UserKeep");

                    using (var client = new HttpClient())
                    {
                        _ = await client.GetStringAsync(url);

                        scope.Span.SetTag(Tags.SamplingPriority, "UserReject");

                        _ = await client.GetStringAsync(url);
                    }
                }
            }
        }
    }
}
