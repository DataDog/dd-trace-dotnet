using System;
using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Exporters.Json;
using Datadog.Trace;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.AppSec;
using Datadog.Trace.BenchmarkDotNet;
using Datadog.Trace.Configuration;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
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

            var api = new Api(settings.Transport.AgentUri, new FakeApiRequestFactory(), statsd: null, updateSampleRates: null, isPartialFlushEnabled: false);

            AgentWriter = new AgentWriter(api, statsd: null, automaticFlush: false);

            var enrichedSpans = new Span[SpanCount];
            var now = DateTimeOffset.UtcNow;

            for (int i = 0; i < SpanCount; i++)
            {
                enrichedSpans[i] = new Span(new SpanContext((ulong)i, (ulong)i, SamplingPriority.UserReject, "Benchmark", null), now);
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
            private readonly IApiRequestFactory _realFactory = new ApiWebRequestFactory();

            public string Info(Uri endpoint)
            {
                return endpoint.ToString();
            }

            public IApiRequest Create(Uri endpoint)
            {
                var request = _realFactory.Create(endpoint);

                return new FakeApiRequest(request);
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

            public Task<IApiResponse> PostAsJsonAsync(IEvent events, JsonSerializer serializer)
            {
                using (var requestStream = Stream.Null)
                {
                    using var streamWriter = new StreamWriter(requestStream);
                    var json = JsonConvert.SerializeObject(events);
                    streamWriter.Write(json);
                }
                return Task.FromResult<IApiResponse>(new FakeApiResponse());
            }

            public async Task<IApiResponse> PostAsync(ArraySegment<byte> traces)
            {
                using (var requestStream = Stream.Null)
                {
                    await requestStream.WriteAsync(traces.Array, traces.Offset, traces.Count).ConfigureAwait(false);
                }

                return new FakeApiResponse();
            }
        }

        private class FakeApiResponse : IApiResponse
        {
            public int StatusCode => 200;

            public long ContentLength => 0;

            public string GetHeader(string headerName) => string.Empty;

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
