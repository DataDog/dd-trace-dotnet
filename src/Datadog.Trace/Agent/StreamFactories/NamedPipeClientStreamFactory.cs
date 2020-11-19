using System.IO;
using System.IO.Pipes;

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

        public NamedPipeClientStreamFactory(string pipeName, string serverName, PipeOptions pipeOptions, int timeoutMs)
        {
            _pipeName = pipeName;
            _serverName = serverName;
            _pipeOptions = pipeOptions;
            _timeoutMs = timeoutMs;
        }

        public string Info()
        {
            return $@"\\{_serverName}\pipe\{_pipeName}";
        }

        public Stream GetBidirectionalStream()
        {
            var pipeStream = new NamedPipeClientStream(_serverName, _pipeName, PipeDirection.InOut, _pipeOptions);
            pipeStream.Connect(_timeoutMs);
            return pipeStream;
        }
    }
}
