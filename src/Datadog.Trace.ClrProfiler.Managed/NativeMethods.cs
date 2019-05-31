using System.Runtime.InteropServices;

namespace Datadog.Trace.ClrProfiler
{
    internal static class NativeMethods
    {
        [DllImport("Datadog.Trace.ClrProfiler.Native")]
        public static extern bool IsProfilerAttached();
    }
}
