using System;
using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    internal enum PW_RET_CODE
    {
        PW_ERR_INTERNAL = -6,
        PW_ERR_TIMEOUT = -5,
        PW_ERR_INVALID_CALL = -4,
        PW_ERR_INVALID_RULE = -3,
        PW_ERR_INVALID_FLOW = -2,
        PW_ERR_NORULE = -1,
        PW_GOOD = 0,
        PW_MONITOR = 1,
        PW_BLOCK = 2
    }
}
