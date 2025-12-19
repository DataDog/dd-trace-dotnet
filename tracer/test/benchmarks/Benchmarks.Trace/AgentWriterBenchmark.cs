using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    public class AgentWriterBenchmark
    {
        private const int SpanCount = 1000;

        private IAgentWriter _agentWriter;
        private IAgentWriter _agentWriterNoOpFlush;
        private SpanCollection _enrichedSpans;

        [GlobalSetup]
        public void GlobalSetup()
        {
            // Create spans in GlobalSetup, not static constructor
            // This ensures BenchmarkDotNet excludes allocation overhead from measurements
            var enrichedSpans = new Span[SpanCount];
            var now = DateTimeOffset.UtcNow;

            for (int i = 0; i < SpanCount; i++)
            {
                var tags = new SqlTags()
                {
                    DbType = "sql-server",
                    InstrumentationName = nameof(IntegrationId.SqlClient),
                };
                enrichedSpans[i] = new Span(new SpanContext((TraceId)i, (ulong)i, SamplingPriorityValues.UserReject, serviceName: "Benchmark", origin: null), now, tags);
                enrichedSpans[i].SetTag("somekey", "Benchmark");
                enrichedSpans[i].SetMetric(Metrics.SamplingRuleDecision, 1.0);
            }

            _enrichedSpans = new SpanCollection(enrichedSpans, SpanCount);

            var config = TracerHelper.DefaultConfig;
            config.Add(ConfigurationKeys.TraceEnabled, false);
            var settings = TracerSettings.Create(config);

            var api = new Api(
                new FakeApiRequestFactory(settings.Manager.InitialExporterSettings.AgentUri),
                new StatsdManager(settings, (_, _) => null!),
                ContainerMetadata.Instance,
                updateSampleRates: null,
                updateConfigState: null,
                partialFlushEnabled: false,
                healthMetricsEnabled: false);

            var noOpStatsd = new StatsdManager(settings, (_, _) => null);
            var noopApi = new NullApi();
            _agentWriter = new AgentWriter(api, statsAggregator: null, statsd: noOpStatsd, automaticFlush: false);
            _agentWriterNoOpFlush = new AgentWriter(noopApi, statsAggregator: null, statsd: noOpStatsd, automaticFlush: false);

            // Warmup to reduce noise
            WriteAndFlushEnrichedTraces().GetAwaiter().GetResult();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _agentWriter.FlushAndCloseAsync();
        }

        /// <summary>
        /// Write realistic traces, but don't flush, to isolate overhead from serialization only
        /// </summary>
        [Benchmark]
        public Task WriteEnrichedTraces()
        {
            _agentWriterNoOpFlush.WriteTrace(_enrichedSpans);
            // Flush os that we clear the buffer
            return _agentWriterNoOpFlush.FlushTracesAsync();
        }

        /// <summary>
        /// Same as WriteAndFlushTraces but with more realistic traces (with tags and metrics)
        /// </summary>
        [Benchmark]
        [BenchmarkCategory(Constants.TracerCategory, Constants.RunOnPrs, Constants.RunOnMaster)]
        public Task WriteAndFlushEnrichedTraces()
        {
            _agentWriter.WriteTrace(_enrichedSpans);
            return _agentWriter.FlushTracesAsync();
        }

        /// <summary>
        /// Try to mimic as much as possible the overhead of the ApiWebRequestFactory,
        /// without actually sending the requests
        /// </summary>
        private class FakeApiRequestFactory : IApiRequestFactory
        {
            private readonly Uri _baseEndpointUri;
            private readonly IApiRequestFactory _realFactory;

            public FakeApiRequestFactory(Uri baseEndpointUri)
            {
                _baseEndpointUri = baseEndpointUri;
                _realFactory = new ApiWebRequestFactory(baseEndpointUri, AgentHttpHeaderNames.DefaultHeaders);
            }

            public string Info(Uri endpoint)
            {
                return endpoint.ToString();
            }

            public Uri GetEndpoint(string relativePath) => UriHelpers.Combine(_baseEndpointUri, relativePath);

            public IApiRequest Create(Uri endpoint)
            {
                var request = _realFactory.Create(endpoint);

                return new FakeApiRequest(request);
            }

            public void SetProxy(WebProxy proxy, NetworkCredential credential)
            {
            }
        }

        private class FakeApiRequest : IApiRequest
        {
            private readonly IApiRequest _realRequest;

            public FakeApiRequest(IApiRequest request)
            {
                _realRequest = request;
            }

            public void AddHeader(string name, string value)
            {
                _realRequest.AddHeader(name, value);
            }

            public Task<IApiResponse> GetAsync()
            {
                return Task.FromResult<IApiResponse>(new FakeApiResponse());
            }

            public Task<IApiResponse> PostAsync(ArraySegment<byte> traces, string contentType)
                => PostAsync(traces, contentType, null);

            public async Task<IApiResponse> PostAsync(ArraySegment<byte> traces, string contentType, string contentEncoding)
            {
                using (var requestStream = Stream.Null)
                {
                    await requestStream.WriteAsync(traces.Array, traces.Offset, traces.Count).ConfigureAwait(false);
                }

                return new FakeApiResponse();
            }

            public Task<IApiResponse> PostAsJsonAsync<T>(T payload, MultipartCompression compression)
                => throw new NotImplementedException();

            public Task<IApiResponse> PostAsJsonAsync<T>(T payload, MultipartCompression compression, JsonSerializerSettings settings)
                => throw new NotImplementedException();

            public async Task<IApiResponse> PostAsync(Func<Stream, Task> writeToRequestStream, string contentType, string contentEncoding, string multipartBoundary)
            {
                using (var requestStream = Stream.Null)
                {
                    await writeToRequestStream(requestStream).ConfigureAwait(false);
                }

                return new FakeApiResponse();
            }

            public Task<IApiResponse> PostAsync(MultipartFormItem[] items, MultipartCompression multipartCompression = MultipartCompression.None)
            {
                throw new NotImplementedException();
            }
        }

        private class FakeApiResponse : IApiResponse
        {
            public int StatusCode => 200;

            public long ContentLength => 0;

            public string ContentTypeHeader => "application/json";

            public string ContentEncodingHeader => null;

            public string GetHeader(string headerName) => string.Empty;

            public Encoding GetCharsetEncoding() => ApiResponseExtensions.GetCharsetEncoding(ContentTypeHeader);

            public ContentEncodingType GetContentEncodingType() => ApiResponseExtensions.GetContentEncodingType(ContentEncodingHeader);

            public Task<Stream> GetStreamAsync()
            {
                throw new NotImplementedException();
            }

            public Task<string> ReadAsStringAsync()
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
            }
        }

            private class NullApi : IApi
            {
                public Task<bool> SendTracesAsync(ArraySegment<byte> traces, int numberOfTraces, bool statsComputationEnabled, long numberOfDroppedP0Traces, long numberOfDroppedP0Spans, bool apmTracingEnabled = true)
                {
                    return Task.FromResult(true);
                }

                public Task<bool> SendStatsAsync(StatsBuffer stats, long bucketDuration)
                {
                    return Task.FromResult(true);
                }
            }
    }
}
