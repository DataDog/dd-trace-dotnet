using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datadog.Tracer
{
    interface IApi
    {
        Task SendTracesAsync(IList<List<Span>> traces);

        Task SendServiceAsync(ServiceInfo service);
    }
}