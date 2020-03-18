using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal interface IApi
    {
        Task SendTracesAsync(Span[][] traces);
    }
}
