using System.Threading.Tasks;

namespace Datadog.Trace.TestHelpers.NamedPipes.Interfaces
{
    public interface ICommunicationClient : ICommunication
    {
        Task<TaskResult> SendMessage(string message);
    }
}
