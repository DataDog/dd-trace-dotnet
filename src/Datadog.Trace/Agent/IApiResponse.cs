using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal interface IApiResponse
    {
        int StatusCode { get; }

        long ContentLength { get; }

        Task<string> ReadAsStringAsync();
    }
}
