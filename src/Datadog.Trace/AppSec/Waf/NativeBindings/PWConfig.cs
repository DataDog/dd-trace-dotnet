using System;
using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    internal struct PWConfig
    {
        public ulong MaxArrayLength;

        public ulong MaxMapDepth;
    }
}
