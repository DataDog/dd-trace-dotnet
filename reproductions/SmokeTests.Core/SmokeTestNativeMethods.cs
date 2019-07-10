using System.Runtime.InteropServices;

namespace SmokeTests.Core
{
    public class SmokeTestNativeMethods
    {
        public static bool IsProfilerAttachedExhaustive()
        {
            try
            {
                return RelativeDll.IsProfilerAttached();
            }
            catch
            {
                try
                {
                    return RelativeSo.IsProfilerAttached();
                }
                catch
                {
                    try
                    {
                        return DockerNativeProjectX64So.IsProfilerAttached();
                    }
                    catch
                    {
                        return DockerNativeProjectX86So.IsProfilerAttached();
                    }
                }
            }
        }

        public static class RelativeDll
        {
            [DllImport("Datadog.Trace.ClrProfiler.Native.dll")]
            public static extern bool IsProfilerAttached();
        }

        public static class RelativeSo
        {
            [DllImport("Datadog.Trace.ClrProfiler.Native.so")]
            public static extern bool IsProfilerAttached();
        }

        public static class DockerNativeProjectX64So
        {
            [DllImport("/project/src/Datadog.Trace.ClrProfiler.Native/bin/Debug/x64/Datadog.Trace.ClrProfiler.Native.so")]
            public static extern bool IsProfilerAttached();
        }

        public static class DockerNativeProjectX86So
        {
            [DllImport("/project/src/Datadog.Trace.ClrProfiler.Native/bin/Debug/x86/Datadog.Trace.ClrProfiler.Native.so")]
            public static extern bool IsProfilerAttached();
        }
    }
}
