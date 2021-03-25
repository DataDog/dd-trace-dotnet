using System;
using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.BenchmarkDotNet;
using Datadog.Trace.Configuration;

namespace Benchmarks.Trace
{
    [DatadogExporter]
    [MemoryDiagnoser]
    public class AgentWriterBenchmark
    {
        private const int SpanCount = 1000;

        private static readonly ITraceWriter AgentWriter;
        private static readonly Span[] Spans;
        private static readonly Span[] EnrichedSpans;

        static AgentWriterBenchmark()
        {
            var settings = TracerSettings.FromDefaultSources();

            settings.StartupDiagnosticLogEnabled = false;
            settings.TraceEnabled = false;

            var api = new Api(settings.AgentUri, new FakeApiRequestFactory(), statsd: null);

            AgentWriter = new AgentWriter(api, new NullMetrics(), automaticFlush: false);

            Spans = new Span[SpanCount];
            EnrichedSpans = new Span[SpanCount];
            var now = DateTimeOffset.UtcNow;

            for (int i = 0; i < SpanCount; i++)
            {
                Spans[i] = new Span(new SpanContext((ulong)i, (ulong)i, SamplingPriority.UserReject, "Benchmark", null), now);
                EnrichedSpans[i] = new Span(new SpanContext((ulong)i, (ulong)i, SamplingPriority.UserReject, "Benchmark", null), now);
                EnrichedSpans[i].SetTag(Tags.Env, "Benchmark");
                EnrichedSpans[i].SetMetric(Metrics.SamplingRuleDecision, 1.0);
            }

            // Run benchmarks once to reduce noise
            new AgentWriterBenchmark().WriteAndFlushTraces().GetAwaiter().GetResult();
            new AgentWriterBenchmark().WriteAndFlushEnrichedTraces().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Write traces to the agent and flushes them
        /// </summary>
        [Benchmark]
        public Task WriteAndFlushTraces()
        {
            AgentWriter.WriteTrace(Spans);
            return AgentWriter.FlushTracesAsync();
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
