using System;
using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.AppSec.Waf
{
    internal enum ReturnCode
    {
        ErrorInternal = -6,
        ErrorTimeout = -5,
        ErrorInvalidCall = -4,
        ErrorInvalidRule = -3,
        ErrorInvalidFlow = -2,
        ErrorNorule = -1,
        Good = 0,
        Monitor = 1,
        Block = 2
    }
}
