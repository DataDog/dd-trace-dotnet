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

        private static readonly IAgentWriter _agentWriter;
        private static readonly Span[] _spans;

        static AgentWriterBenchmark()
        {
            TracerSettings.DisableSharedInstance = true;

            var settings = TracerSettings.FromDefaultSources();

            settings.TraceEnabled = false;

            var api = new Api(settings.AgentUri, new FakeHttpHandler() , statsd: null);

            _agentWriter = new AgentWriter(api, statsd: null, automaticFlush: false);

            _spans = new Span[SpanCount];
            var now = DateTimeOffset.UtcNow;

            for (int i = 0; i < SpanCount; i++)
            {
                _spans[i] = new Span(new SpanContext((ulong)i, (ulong)i, SamplingPriority.UserReject, "Benchmark"), now);
            }
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
