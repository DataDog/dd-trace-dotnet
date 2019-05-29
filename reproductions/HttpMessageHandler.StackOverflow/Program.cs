using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler;

namespace HttpMessageHandler.StackOverflow
{
    internal class Program
    {
        private static async Task Main()
        {
            Console.WriteLine($"Profiler attached: {Instrumentation.ProfilerAttached}");

            var baseAddress = new Uri("https://www.example.com/");
            var regularHttpClient = new HttpClient { BaseAddress = baseAddress };
            var customHandlerHttpClient = new HttpClient(new DerivedHandler()) { BaseAddress = baseAddress };

            using (var scope = Tracer.Instance.StartActive("main"))
            {
                Console.WriteLine("Calling regularHttpClient.GetAsync");
                await regularHttpClient.GetAsync("default-handler");
                Console.WriteLine("Called regularHttpClient.GetAsync");

                Console.WriteLine("Calling customHandlerHttpClient.GetAsync");
                await customHandlerHttpClient.GetAsync("derived-handler");
                Console.WriteLine("Called customHandlerHttpClient.GetAsync");
            }
        }
    }

    public class DerivedHandler : HttpClientHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Tracer.Instance.ActiveScope?.Span.SetTag("class", nameof(DerivedHandler));

            Console.WriteLine("Calling base.SendAsync()");
            var result = await base.SendAsync(request, cancellationToken);
            Console.WriteLine("Called base.SendAsync()");
            return result;
        }
    }
}
