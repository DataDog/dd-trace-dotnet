using System;
using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.AppSec.Waf
{
    internal interface IPowerWaf : IDisposable
    {
        public IAdditiveContext CreateAdditiveContext();
    }
}
