using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal interface IApiRequestFactory
    {
        IApiRequest Create(Uri endpoint);
    }
}
