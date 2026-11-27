using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
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
    [IterationTime(500)]
    public class AgentWriterBenchmark
    {
        private const int SpanCount = 1000;

        // Same default buffer size as the AgentWriter
        private const int MaxBufferSize = 1024 * 1024 * 10;

        private Api _api;
        private SpanBuffer _buffer;
        private byte[] _temporaryBuffer;
        private byte[] _spareBuffer;
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

            _api = new Api(
                new FakeApiRequestFactory(settings.Manager.InitialExporterSettings.AgentUri),
                new StatsdManager(settings, (_, _) => null!),
                ContainerMetadata.Instance,
                updateSampleRates: null,
                updateConfigState: null,
                partialFlushEnabled: false,
                healthMetricsEnabled: false);

            // Drive the `AgentWriter`'s `SpanBuffer` and serializer directly. An `AgentWriter`
            // always starts its serialization and flush loops, so awaiting `FlushTracesAsync()`
            // would time that background work instead of the write and flush path.
            _buffer = new SpanBuffer(MaxBufferSize, new SpanBufferMessagePackSerializer(SpanFormatterResolver.Instance));
            _temporaryBuffer = new byte[1024];
        }

        /// <summary>
        /// Write realistic traces, but don't send them to the API: measures serialization
        /// plus the detach half of a flush (the only way to reset the buffer)
        /// </summary>
        [Benchmark]
        public void WriteEnrichedTraces()
        {
            WriteEnrichedTracesToBuffer();
            DetachPayload();
        }

        /// <summary>
        /// Same as WriteAndFlushTraces but with more realistic traces (with tags and metrics)
        /// </summary>
        [Benchmark]
        [BenchmarkCategory(Constants.TracerCategory, Constants.RunOnPrs, Constants.RunOnMaster)]
        public Task WriteAndFlushEnrichedTraces()
        {
            WriteEnrichedTracesToBuffer();

            var payload = DetachPayload();
            return _api.SendTracesAsync(payload.Data, payload.TraceCount, statsComputationEnabled: false, numberOfDroppedP0Traces: 0, numberOfDroppedP0Spans: 0);
        }

        private void WriteEnrichedTracesToBuffer()
        {
            if (_buffer.TryWrite(in _enrichedSpans, ref _temporaryBuffer) is not SpanBuffer.WriteStatus.Success)
            {
                throw new InvalidOperationException("The trace chunk did not fit in the buffer");
            }
        }

        // Mirrors the sizing half of `AgentWriter.FlushBuffer`: size the replacement array before
        // detaching, then recycle the detached array. Unlike `FlushBuffer`, the recycled array
        // aliases the payload until the send task completes. This is safe only because
        // BenchmarkDotNet awaits each invocation before starting the next one.
        private SpanBuffer.Payload DetachPayload()
        {
            if (_spareBuffer is null || _spareBuffer.Length < _buffer.CurrentLength)
            {
                _spareBuffer = new byte[_buffer.CurrentLength];
            }

            var payload = _buffer.Detach(_spareBuffer);
            _spareBuffer = payload.Array ?? _spareBuffer;
            return payload;
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

            public Uri GetEndpoint(string relativePath) => relativePath is null ? _baseEndpointUri : UriHelpers.Combine(_baseEndpointUri, relativePath);

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
    }
}
