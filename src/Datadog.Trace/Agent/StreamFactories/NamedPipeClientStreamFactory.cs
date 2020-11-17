using System.IO;
using System.IO.Pipes;
using System.Security.Principal;

namespace Datadog.Trace.Agent.StreamFactories
{
    internal class NamedPipeClientStreamFactory : IStreamFactory
    {
        private readonly string _pipeName;
        private readonly string _serverName;
        private readonly PipeOptions _pipeOptions;
        private readonly int _timeoutMs;

        public NamedPipeClientStreamFactory(string pipeName, int timeoutMs)
            : this(pipeName, ".", PipeOptions.Asynchronous, timeoutMs)
        {
        }

        public NamedPipeClientStreamFactory(string pipeName, string serverName, PipeOptions pipeOptions, int timeoutMs, TokenImpersonationLevel impersonationLevel = TokenImpersonationLevel.Identification)
        {
            _pipeName = pipeName;
            _serverName = serverName;
            _pipeOptions = pipeOptions;
            _timeoutMs = timeoutMs;
        }

        public void GetStreams(out Stream requestStream, out Stream responseStream)
        {
            var pipeStream = new NamedPipeClientStream(_serverName, _pipeName, PipeDirection.InOut, _pipeOptions);
            pipeStream.Connect(_timeoutMs);
            requestStream = pipeStream;
            responseStream = pipeStream;
        }
    }
}
