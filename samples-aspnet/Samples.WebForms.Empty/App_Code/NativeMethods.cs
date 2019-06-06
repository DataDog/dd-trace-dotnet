using System.Runtime.InteropServices;

namespace Samples.WebForms.Empty
{
    public static class NativeMethods
    {
        [DllImport("Datadog.Trace.ClrProfiler.Native.dll")]
        public static extern bool IsProfilerAttached();
    }
}
