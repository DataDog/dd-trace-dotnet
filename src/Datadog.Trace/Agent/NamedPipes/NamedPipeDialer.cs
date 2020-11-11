using System;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent.NamedPipes
{
    internal class NamedPipeDialer : IDial
    {
        private readonly string _pipeName;
        private readonly string _serverName;
        private readonly PipeOptions _pipeOptions;
        private readonly int _timeoutMs;

        public NamedPipeDialer(string pipeName)
            : this(pipeName, ".", PipeOptions.Asynchronous, 0)
        {
        }

        public NamedPipeDialer(string pipeName, int timeoutMs)
            : this(pipeName, ".", PipeOptions.Asynchronous, timeoutMs)
        {
        }

        public NamedPipeDialer(string pipeName, string serverName, PipeOptions pipeOptions, int timeoutMs, TokenImpersonationLevel impersonationLevel = TokenImpersonationLevel.Identification)
        {
            _pipeName = pipeName;
            _serverName = serverName;
            _pipeOptions = pipeOptions;
            _timeoutMs = timeoutMs;
        }

        public Task<NamedPipeClientStream> DialAsync(TraceRequest request, CancellationToken cancellationToken)
        {
            var pipeStream = new NamedPipeClientStream(_serverName, _pipeName, PipeDirection.InOut, _pipeOptions);

            pipeStream.Connect(_timeoutMs);

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                    {
                        try
                        {
                            pipeStream.Dispose();
                        }
                        catch (Exception) { }
                    });
            }

            return Task.FromResult(pipeStream);
        }
    }
}
