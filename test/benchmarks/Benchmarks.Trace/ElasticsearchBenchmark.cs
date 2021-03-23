using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch.V6;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Integrations;
using Datadog.Trace.Configuration;
using Elasticsearch.Net;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    public class ElasticsearchBenchmark
    {
        private static readonly int MdToken;
        private static readonly IntPtr GuidPtr;
        private static readonly RequestPipeline Pipeline = new RequestPipeline();
        private static readonly object PipelineObject = new RequestPipeline();
        private static readonly RequestData Data = new RequestData
        {
            Method = HttpMethod.POST,
            Uri = new Uri("http://localhost/"),
            PathAndQuery = "PathAndQuery"
        };

        static ElasticsearchBenchmark()
        {
            var settings = new TracerSettings
            {
                StartupDiagnosticLogEnabled = false
            };

            Tracer.Instance = new Tracer(settings, new DummyAgentWriter(), null, null, null);

            var methodInfo = typeof(RequestPipeline).GetMethod("CallElasticsearchAsync", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            MdToken = methodInfo.MetadataToken;
            var guid = typeof(RequestPipeline).Module.ModuleVersionId;

            GuidPtr = Marshal.AllocHGlobal(Marshal.SizeOf(guid));

            Marshal.StructureToPtr(guid, GuidPtr, false);

            var bench = new ElasticsearchBenchmark();
            bench.CallElasticsearch();
            bench.CallElasticsearchAsync();
            bench.CallTargetCallElasticsearch();
            bench.CallTargetCallElasticsearchAsync();
        }

        [Benchmark]
        public object CallElasticsearch()
        {
            return ElasticsearchNet6Integration.CallElasticsearch<int>(
                PipelineObject,
                Data,
                (int)OpCodeValue.Callvirt,
                MdToken,
                (long)GuidPtr);
        }

        [Benchmark]
        public int CallElasticsearchAsync()
        {
            var task = (Task<int>)ElasticsearchNet6Integration.CallElasticsearchAsync<int>(
                PipelineObject,
                Data,
                CancellationToken.None,
                (int)OpCodeValue.Callvirt,
                MdToken,
                (long)GuidPtr);

            return task.GetAwaiter().GetResult();
        }

        [Benchmark]
        public unsafe object CallTargetCallElasticsearch()
        {
            return CallTarget.Run<RequestPipeline_CallElasticsearch_Integration, RequestPipeline, RequestData, int>(Pipeline, Data, &GetData);

            static int GetData(RequestData data) => default;
        }


        [Benchmark]
        public unsafe int CallTargetCallElasticsearchAsync()
        {
            return CallTarget.Run<RequestPipeline_CallElasticsearchAsync_Integration, RequestPipeline, RequestData, CancellationToken, Task<int>>
                (Pipeline, Data, CancellationToken.None, &GetData).GetAwaiter().GetResult();

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
