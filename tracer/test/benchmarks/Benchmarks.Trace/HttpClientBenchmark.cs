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
        private HttpRequestMessage _httpRequest;
        private Task<HttpResponseMessage> _cachedResult;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var settings = TracerSettings.Create(new() { { ConfigurationKeys.StartupDiagnosticLogEnabled, false } });

            Tracer.UnsafeSetTracerInstance(new Tracer(settings, new DummyAgentWriter(), null, null, null));

            _httpRequest = new HttpRequestMessage { RequestUri = new Uri("http://datadoghq.com") };
            _cachedResult = Task.FromResult(new HttpResponseMessage());

            // Warmup
            SendAsync();
        }

        [Benchmark]
        public unsafe string SendAsync()
        {
            CallTarget.Run<HttpClientHandlerIntegration, HttpClientBenchmark, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>
                (this, _httpRequest, CancellationToken.None, &GetResult).GetAwaiter().GetResult();
            return "OK";

            Task<HttpResponseMessage> GetResult(HttpRequestMessage request, CancellationToken cancellationToken) => _cachedResult;
        }
    }
}
