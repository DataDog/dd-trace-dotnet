using System;
using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
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

        static AgentWriterBenchmark()
        {
            var settings = TracerSettings.FromDefaultSources();

            settings.StartupDiagnosticLogEnabled = false;
            settings.TraceEnabled = false;

            var api = new Api(settings.AgentUri, new FakeApiRequestFactory(), statsd: null);

            AgentWriter = new AgentWriter(api, statsd: null, automaticFlush: false);

            Spans = new Span[SpanCount];
            var now = DateTimeOffset.UtcNow;

            for (int i = 0; i < SpanCount; i++)
            {
                Spans[i] = new Span(new SpanContext((ulong)i, (ulong)i, SamplingPriority.UserReject, "Benchmark", null), now);
            }
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
        /// Try to mimick as much as possible the overhead of the ApiWebRequestFactory,
        /// without actually sending the requests
        /// </summary>
        private class FakeApiRequestFactory : IApiRequestFactory
        {
            private readonly IApiRequestFactory _realFactory = new ApiWebRequestFactory();

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

            public async Task<IApiResponse> PostAsync(Span[][] traces, FormatterResolverWrapper formatterResolver)
            {
                using (var requestStream = Stream.Null)
                {
                    await CachedSerializer.Instance.SerializeAsync(requestStream, traces, formatterResolver).ConfigureAwait(false);
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
