using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datadog.Tracer
{
    interface IApi
    {
        Task SendTracesAsync(List<List<Span>> traces);

        Task SendServiceAsync(ServiceInfo service);
    }
}