using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datadog.Trace
{
    interface IApi
    {
        Task SendTracesAsync(IList<List<Span>> traces);

        Task SendServiceAsync(ServiceInfo service);
    }
}