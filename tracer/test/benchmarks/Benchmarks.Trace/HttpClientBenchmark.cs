using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient.HttpClientHandler;
using Datadog.Trace.Configuration;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    [BenchmarkAgent3]
    public class HttpClientBenchmark
    {
        private static readonly HttpRequestMessage HttpRequest = new HttpRequestMessage { RequestUri = new Uri("http://datadoghq.com") };

        private static readonly Task<HttpResponseMessage> CachedResult = Task.FromResult(new HttpResponseMessage());

        static HttpClientBenchmark()
        {
            var settings = new TracerSettings
            {
                StartupDiagnosticLogEnabled = false
            };

            Tracer.UnsafeSetTracerInstance(new Tracer(settings, new DummyAgentWriter(), null, null, null));

            new HttpClientBenchmark().SendAsync().GetAwaiter().GetResult();
        }

        [Benchmark]
        public async Task<string> SendAsync()
        {
            await CallTargetRun().ConfigureAwait(false);
            return "OK";

            unsafe Task<HttpResponseMessage> CallTargetRun()
                => CallTarget.Run<HttpClientHandlerIntegration, HttpClientBenchmark, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>(this, HttpRequest, CancellationToken.None, &GetResult);

            static Task<HttpResponseMessage> GetResult(HttpRequestMessage request, CancellationToken cancellationToken)
                => CachedResult;
        }
    }
}
