using System;
using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Datadog.Trace;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.BenchmarkDotNet;
using Datadog.Trace.Configuration;
using Datadog.Trace.Vendors.MessagePack;

namespace Benchmarks.Trace
{
    [DatadogExporter]
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net472)]
    [SimpleJob(RuntimeMoniker.NetCoreApp31)]
    public class AgentWriterBenchmark
    {
        private const int SpanCount = 1000;

        private static readonly IAgentWriter _agentWriter;
        private static readonly Span[] _spans;

        static AgentWriterBenchmark()
        {
            var settings = TracerSettings.FromDefaultSources();

            settings.StartupDiagnosticLogEnabled = false;
            settings.TraceEnabled = false;

            var api = new Api(settings.AgentUri, new FakeApiRequestFactory(), statsd: null);

            _agentWriter = new AgentWriter(api, statsd: null, automaticFlush: false);

            _spans = new Span[SpanCount];
            var now = DateTimeOffset.UtcNow;

            for (int i = 0; i < SpanCount; i++)
            {
                _spans[i] = new Span(new SpanContext((ulong)i, (ulong)i, SamplingPriority.UserReject, "Benchmark", null), now);
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
                using (var requestStream = new MemoryStream())
                {
                    await MessagePackSerializer.SerializeAsync(requestStream, traces, formatterResolver).ConfigureAwait(false);
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
