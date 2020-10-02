using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.Agent;
using Datadog.Trace.BenchmarkDotNet;
using Datadog.Trace.Configuration;

namespace Benchmarks.Trace
{
    [DatadogExporter]
    [MemoryDiagnoser]
    public class AgentWriterBenchmark
    {
        private const int SpanCount = 1000;

        private static readonly IAgentWriter AgentWriter;
        private static readonly Span[] Spans;
        private static readonly Span[] EnrichedSpans;

        static AgentWriterBenchmark()
        {
            TracerSettings.DisableSharedInstance = true;

            var settings = TracerSettings.FromDefaultSources();

            settings.TraceEnabled = false;
            settings.DiagnosticSourceEnabled = false;

            var api = new Api(settings.AgentUri, new FakeHttpHandler() , statsd: null);

            _agentWriter = new AgentWriter(api, statsd: null, automaticFlush: false);

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
            _agentWriter.WriteTrace(_spans);
            return _agentWriter.FlushTracesAsync();
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

        private class FakeHttpHandler : DelegatingHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                await request.Content.CopyToAsync(Stream.Null);

                return new HttpResponseMessage();
            }
        }
    }
}
