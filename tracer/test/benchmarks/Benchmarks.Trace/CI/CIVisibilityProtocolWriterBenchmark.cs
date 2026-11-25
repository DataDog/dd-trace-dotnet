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
        /// Same as WriteAndFlushTraces but with more realistic traces (with tags and metrics)
        /// </summary>
        [Benchmark]
        public int WriteAndFlushEnrichedTraces()
        {
            // CIVisibilityProtocolWriter enqueues events and lets background flush tasks
            // serialize and send them, which makes the timed region measure queue handoff
            // and task scheduling instead of the protocol writing path. Run the same work
            // the flush loop performs (event transform, payload serialization, flush+reset)
            // inline so the measurement is synchronous and self-contained.
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
