using System.Runtime.InteropServices;

namespace Datadog.Trace.ClrProfiler
{
    internal static class NativeMethods
    {
        public static class Windows
        {
            [DllImport("Datadog.Trace.ClrProfiler.Native.dll")]
            public static extern bool IsProfilerAttached();
        }

        public static class Linux
        {
            [DllImport("Datadog.Trace.ClrProfiler.Native.so")]
            public static extern bool IsProfilerAttached();
        }
    }
}
