using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.Agent;
using Datadog.Trace.Ci.Agent.MessagePack;
using Datadog.Trace.Ci.Agent.Payloads;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    [BenchmarkCategory(Constants.TracerCategory, Constants.RunOnPrs, Constants.RunOnMaster)]
    [IterationTime(500)]
    public class CIVisibilityProtocolWriterBenchmark
    {
        private const int SpanCount = 1000;

        private CITestCyclePayload _payload;
        private SpanCollection _enrichedSpans;

        [GlobalSetup]
        public void GlobalSetup()
        {
            // Create spans in GlobalSetup, not static constructor
            // This ensures BenchmarkDotNet excludes allocation overhead from measurements
            var enrichedSpans = new Span[SpanCount];
            var now = DateTimeOffset.UtcNow;
            for (var i = 0; i < SpanCount; i++)
            {
                enrichedSpans[i] = new Span(new SpanContext((TraceId)i, (ulong)i, SamplingPriorityValues.UserReject, serviceName: "Benchmark", origin: null), now);
                enrichedSpans[i].SetTag(Tags.Env, "Benchmark");
                enrichedSpans[i].SetMetric(Metrics.SamplingRuleDecision, 1.0);
            }

            _enrichedSpans = new SpanCollection(enrichedSpans, SpanCount);

            var config = TracerHelper.DefaultConfig;
            config[ConfigurationKeys.TraceEnabled] = false;
            var settings = new TestOptimizationSettings(new DictionaryObjectConfigurationSource(config), NullConfigurationTelemetry.Instance);

            _payload = new CITestCyclePayload(settings, CIFormatterResolver.Instance);
        }

        /// <summary>
        /// Synchronously writes enriched spans (with tags and metrics) into the CI test-cycle
        /// payload and flushes it, mirroring the CIVisibilityProtocolWriter flush loop.
        /// </summary>
        [Benchmark]
        public int WriteAndFlushEnrichedTraces()
        {
            // `CIVisibilityProtocolWriter` hands events to background flush tasks, so timing it
            // measures queue handoff and task scheduling. Run the flush loop's work (event
            // transform, payload serialization, flush and reset) inline instead.
            foreach (var span in _enrichedSpans)
            {
                var @event = CIVisibilityEventsFactory.FromSpan(span);
                while (!_payload.TryProcessEvent(@event) && _payload.HasEvents)
                {
                    FlushPayload();
                }
            }

            return FlushPayload();
        }

        private int FlushPayload()
        {
            var bytesWritten = _payload.WriteTo(Stream.Null);
            _payload.Reset();
            return bytesWritten;
        }
    }
}
