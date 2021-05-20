using System;
using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.AppSec.Waf
{
    internal enum ArgsType
    {
        Invalid = 0,
        SignedNumber = 1 << 0,
        UnsignedNumber = 1 << 1,
        String = 1 << 2,
        Array = 1 << 3,
        Map = 1 << 4,
    }
}
