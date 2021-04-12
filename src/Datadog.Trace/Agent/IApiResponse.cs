using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal interface IApiResponse : IDisposable
    {
        int StatusCode { get; }

        long ContentLength { get; }

        string GetHeader(string headerName);

        Task<string> ReadAsStringAsync();
    }
}
