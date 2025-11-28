using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch.V6;
using Datadog.Trace.Configuration;
using Elasticsearch.Net;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    [BenchmarkCategory(Constants.TracerCategory, Constants.RunOnPrs, Constants.RunOnMaster)]
    public class ElasticsearchBenchmark
    {
        private RequestPipeline _pipeline;
        private RequestData _data;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var settings = TracerSettings.Create(new() { { ConfigurationKeys.StartupDiagnosticLogEnabled, false } });

            Tracer.UnsafeSetTracerInstance(new Tracer(settings, new DummyAgentWriter(), null, null, null));

            _pipeline = new RequestPipeline();
            _data = new RequestData
            {
                Method = HttpMethod.POST,
                Uri = new Uri("http://localhost/"),
                PathAndQuery = "PathAndQuery"
            };
        }

        [Benchmark]
        public unsafe object CallElasticsearch()
        {
            return CallTarget.Run<RequestPipeline_CallElasticsearch_Integration, RequestPipeline, RequestData, int>(_pipeline, _data, &GetData);

            static int GetData(RequestData data) => default;
        }


        [Benchmark]
        public unsafe int CallElasticsearchAsync()
        {
            return CallTarget.Run<RequestPipeline_CallElasticsearchAsync_Integration, RequestPipeline, RequestData, CancellationToken, Task<int>>
                (_pipeline, _data, CancellationToken.None, &GetData).GetAwaiter().GetResult();

            static Task<int> GetData(RequestData data, CancellationToken cancellationToken) => Task.FromResult<int>(default);
        }
    }
}

namespace Elasticsearch.Net
{
    public class RequestPipeline
    {
        private object RequestParameters { get; } = "Parameters";

        public T CallElasticsearch<T>(RequestData requestData)
        {
            return default;
        }

        public Task<T> CallElasticsearchAsync<T>(RequestData requestData, CancellationToken cancellationToken)
        {
            return Task.FromResult(default(T));
        }
    }

    public class RequestData
    {
        public Uri Uri { get; set; }
        public string PathAndQuery { get; set; }
        public HttpMethod Method { get; set; }
    }

    public enum HttpMethod
    {
        GET,
        POST,
        PUT,
        DELETE,
        HEAD,
    }
}
