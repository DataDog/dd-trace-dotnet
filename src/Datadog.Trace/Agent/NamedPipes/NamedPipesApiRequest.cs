using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.MessagePack;

namespace Datadog.Trace.Agent
{
    internal class NamedPipesApiRequest : IApiRequest
    {
        private readonly TraceRequest _request;
        private readonly string _pipeName;
        private readonly int _timeoutMs;
        private readonly CancellationToken _cancellationToken;
        private readonly NamedPipeDialer _dialer;
        private readonly DialMessageHandler _handler;

        public NamedPipesApiRequest(string pipeName, int timeoutMs, CancellationToken cancellationToken)
        {
            _pipeName = pipeName;
            _timeoutMs = timeoutMs;
            _cancellationToken = cancellationToken;
            _dialer = new NamedPipeDialer(_pipeName, ".", System.IO.Pipes.PipeOptions.Asynchronous, _timeoutMs);
            _handler = new DialMessageHandler(_dialer);
            _request = new TraceRequest();
        }

        public void AddHeader(string name, string value)
        {
            _request.Headers.Add(new KeyValuePair<string, string>(name, value));
        }

        public async Task<IApiResponse> PostAsync(Span[][] traces, FormatterResolverWrapper formatterResolver)
        {
            _request.Traces = traces;
            var response = await _handler.SendAsync(_request, _cancellationToken);
            return response;
        }
    }
}
