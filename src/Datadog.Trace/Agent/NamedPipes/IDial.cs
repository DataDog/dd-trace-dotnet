using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent.NamedPipes
{
    internal interface IDial
    {
        Task<NamedPipeClientStream> DialAsync(TraceRequest request, CancellationToken cancellationToken);
    }
}
