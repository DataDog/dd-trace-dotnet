using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datadog.Trace
{
    internal interface IApi
    {
        Task SendTracesAsync(IList<List<SpanBase>> traces);

        Task SendServiceAsync(ServiceInfo service);
    }
}