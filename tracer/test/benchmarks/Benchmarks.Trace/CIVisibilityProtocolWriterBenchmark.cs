using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.Ci.Agent;
using Datadog.Trace.Ci.Agent.Payloads;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    [BenchmarkCategory(Constants.TracerCategory)]
    public class CIVisibilityProtocolWriterBenchmark
    {
        private const int SpanCount = 1000;

        private IEventWriter _eventWriter;
        private ArraySegment<Span> _enrichedSpans;

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

            _enrichedSpans = new ArraySegment<Span>(enrichedSpans);

            var overrides = new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.StartupDiagnosticLogEnabled, false.ToString() },
                { ConfigurationKeys.TraceEnabled, false.ToString() },
            });
            var sources = new CompositeConfigurationSource(new[] { overrides, GlobalConfigurationSource.Instance });
            var settings = new TestOptimizationSettings(sources, NullConfigurationTelemetry.Instance);

            _eventWriter = new CIVisibilityProtocolWriter(settings, new FakeCIVisibilityProtocolWriter());

            // Warmup to reduce noise
            WriteAndFlushEnrichedTraces().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Same as WriteAndFlushTraces but with more realistic traces (with tags and metrics)
        /// </summary>
        [Benchmark]
        public Task WriteAndFlushEnrichedTraces()
        {
            _eventWriter.WriteTrace(_enrichedSpans);
            return _eventWriter.FlushTracesAsync();
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
