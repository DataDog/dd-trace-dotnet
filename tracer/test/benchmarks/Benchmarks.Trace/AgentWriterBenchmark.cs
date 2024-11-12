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
using Datadog.Trace.Util;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    [BenchmarkAgent1]
    [BenchmarkCategory(Constants.TracerCategory)]

    public class AgentWriterBenchmark
    {
        private const int SpanCount = 1000;

        private static readonly IAgentWriter AgentWriter;
        private static readonly ArraySegment<Span> EnrichedSpans;
        static AgentWriterBenchmark()
        {
            var settings = TracerSettings.FromDefaultSources();

            settings.StartupDiagnosticLogEnabled = false;
            settings.TraceEnabled = false;

            var api = new Api(new FakeApiRequestFactory(settings.Exporter.AgentUri), statsd: null, updateSampleRates: null, partialFlushEnabled: false);

            AgentWriter = new AgentWriter(api, statsAggregator: null, statsd: null, automaticFlush: false);

            var enrichedSpans = new Span[SpanCount];
            var now = DateTimeOffset.UtcNow;

            for (int i = 0; i < SpanCount; i++)
            {
                enrichedSpans[i] = new Span(new SpanContext((TraceId)i, (ulong)i, SamplingPriorityValues.UserReject, serviceName: "Benchmark", origin: null), now);
                enrichedSpans[i].SetTag(Tags.Env, "Benchmark");
                enrichedSpans[i].SetMetric(Metrics.SamplingRuleDecision, 1.0);
            }

            EnrichedSpans = new ArraySegment<Span>(enrichedSpans);

            // Run benchmarks once to reduce noise
            new AgentWriterBenchmark().WriteAndFlushEnrichedTraces().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Same as WriteAndFlushTraces but with more realistic traces (with tags and metrics)
        /// </summary>
        [Benchmark]
        public Task WriteAndFlushEnrichedTraces()
        {
            AgentWriter.WriteTrace(EnrichedSpans);
            return AgentWriter.FlushTracesAsync();
        }

        /// <summary>
        /// Try to mimick as much as possible the overhead of the ApiWebRequestFactory,
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
    }
}
