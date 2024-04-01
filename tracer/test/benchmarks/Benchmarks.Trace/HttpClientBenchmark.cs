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
    [BenchmarkCategory(Constants.TracerCategory)]
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

            var bench = new HttpClientBenchmark();
            bench.SendAsync();
        }

        [Benchmark]
        public unsafe string SendAsync()
        {
            CallTarget.Run<HttpClientHandlerIntegration, HttpClientBenchmark, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>
                (this, HttpRequest, CancellationToken.None, &GetResult).GetAwaiter().GetResult();
            return "OK";

            static Task<HttpResponseMessage> GetResult(HttpRequestMessage request, CancellationToken cancellationToken) => CachedResult;
        }
    }
}
