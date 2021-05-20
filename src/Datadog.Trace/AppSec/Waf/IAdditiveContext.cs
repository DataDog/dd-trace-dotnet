using System;
using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.AppSec.Waf
{
    internal interface IAdditiveContext : IDisposable
    {
        Return Run(IReadOnlyDictionary<string, object> args);
    }
}
