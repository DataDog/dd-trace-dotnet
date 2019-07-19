using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal interface IAgentWriter
    {
        Task FlushAndCloseAsync();
    }
}
