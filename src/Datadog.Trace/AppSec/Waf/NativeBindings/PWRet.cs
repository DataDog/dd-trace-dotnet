using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct PWRet
    {
        public PW_RET_CODE Action;

        public IntPtr Data;

        public IntPtr PerfData;

        public int PerfTotalRuntime;

        public int PerfCacheHitRate;
    }
}
