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
    [BenchmarkCategory(Constants.TracerCategory, Constants.RunOnPrs, Constants.RunOnMaster)]
    public class HttpClientBenchmark
    {
        private HttpRequestMessage _httpRequest;
        private static readonly Task<HttpResponseMessage> _cachedResult = Task.FromResult(new HttpResponseMessage());

        [GlobalSetup]
        public void GlobalSetup()
        {
            TracerHelper.SetGlobalTracer();
            _httpRequest = new HttpRequestMessage { RequestUri = new Uri("http://datadoghq.com") };
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            TracerHelper.CleanupGlobalTracer();
            _httpRequest.Dispose();
        }

        [Benchmark]
        public unsafe string SendAsync()
        {
            CallTarget.Run<HttpClientHandlerIntegration, HttpClientBenchmark, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>
                (this, _httpRequest, CancellationToken.None, &GetResult).GetAwaiter().GetResult();
            return "OK";

            static Task<HttpResponseMessage> GetResult(HttpRequestMessage request, CancellationToken cancellationToken) => _cachedResult;
        }
    }
}
