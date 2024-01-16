using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.Ci.Agent;
using Datadog.Trace.Ci.Agent.Payloads;
using Datadog.Trace.Ci.Configuration;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    [BenchmarkAgent1]
    [BenchmarkCategory(Constants.TracerCategory)]
    public class CIVisibilityProtocolWriterBenchmark
    {
        private const int SpanCount = 1000;

        private static readonly IEventWriter EventWriter;
        private static readonly ArraySegment<Span> EnrichedSpans;

        static CIVisibilityProtocolWriterBenchmark()
        {
            var settings = CIVisibilitySettings.FromDefaultSources();
            settings.TracerSettings.StartupDiagnosticLogEnabled = false;
            settings.TracerSettings.TraceEnabled = false;

            EventWriter = new CIVisibilityProtocolWriter(settings, new FakeCIVisibilityProtocolWriter());

            var enrichedSpans = new Span[SpanCount];
            var now = DateTimeOffset.UtcNow;
            for (var i = 0; i < SpanCount; i++)
            {
                enrichedSpans[i] = new Span(new SpanContext((TraceId)i, (ulong)i, SamplingPriorityValues.UserReject, serviceName: "Benchmark", origin: null), now);
                enrichedSpans[i].SetTag(Tags.Env, "Benchmark");
                enrichedSpans[i].SetMetric(Metrics.SamplingRuleDecision, 1.0);
            }

            EnrichedSpans = new ArraySegment<Span>(enrichedSpans);

            // Run benchmarks once to reduce noise
            new CIVisibilityProtocolWriterBenchmark().WriteAndFlushEnrichedTraces().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Same as WriteAndFlushTraces but with more realistic traces (with tags and metrics)
        /// </summary>
        [Benchmark]
        public Task WriteAndFlushEnrichedTraces()
        {
            EventWriter.WriteTrace(EnrichedSpans);
            return EventWriter.FlushTracesAsync();
        }

        private class FakeCIVisibilityProtocolWriter : ICIVisibilityProtocolWriterSender
        {
            public Task SendPayloadAsync(EventPlatformPayload payload)
            {
                return Task.CompletedTask;
            }
        }
    }
}
