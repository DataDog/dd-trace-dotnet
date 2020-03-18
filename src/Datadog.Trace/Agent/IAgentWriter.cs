using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal interface IAgentWriter
    {
        void WriteTrace(Span[] trace);

        Task FlushAndCloseAsync();

        void OverrideApi(IApi api);
    }
}
