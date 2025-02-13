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
using Datadog.Trace.LibDatadog;
using Datadog.Trace.Util;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    [BenchmarkAgent8]
    [BenchmarkCategory(Constants.TracerCategory)]

    public class AgentWriterBenchmarkWithApi
    {
        private const int SpanCount = 1000;

        private static readonly IAgentWriter AgentWriter;
        private static readonly ArraySegment<Span> EnrichedSpans;
        static AgentWriterBenchmarkWithApi()
        {
            var overrides = new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.StartupDiagnosticLogEnabled, false.ToString() },
                { ConfigurationKeys.TraceEnabled, false.ToString() },
            });
            var sources = new CompositeConfigurationSource(new[] { overrides, GlobalConfigurationSource.Instance });
            var settings = new TracerSettings(sources);

            var apiRequestFactory = TracesTransportStrategy.Get(settings.Exporter);
            var api = new Api(apiRequestFactory, statsd: null, updateSampleRates: null, partialFlushEnabled: false);

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
    }
}
