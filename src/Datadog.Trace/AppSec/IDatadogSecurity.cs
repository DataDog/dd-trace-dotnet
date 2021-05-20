using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.AppSec
{
    internal interface IDatadogSecurity : IDisposable
    {
        bool Enabled { get; }

        InstrumentationGateway InstrumentationGateway { get; }
    }
}
