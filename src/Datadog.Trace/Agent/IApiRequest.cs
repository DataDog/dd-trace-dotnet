using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal interface IApiRequest
    {
        void AddHeader(string name, string value);

        Task<IApiResponse> PostAsync(ArraySegment<byte> traces);
    }
}
