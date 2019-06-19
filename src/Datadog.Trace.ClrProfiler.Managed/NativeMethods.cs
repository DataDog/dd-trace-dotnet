using System;
using System.Runtime.InteropServices;

// ReSharper disable MemberHidesStaticFromOuterClass
namespace Datadog.Trace.ClrProfiler
{
    internal static class NativeMethods
    {
        public static bool IsProfilerAttached()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Windows.IsProfilerAttached();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return Linux.IsProfilerAttached();
            }

            throw new PlatformNotSupportedException();
        }

        private static class Windows
        {
            [DllImport("Datadog.Trace.ClrProfiler.Native.dll")]
            public static extern bool IsProfilerAttached();
        }

        private static class Linux
        {
            [DllImport("Datadog.Trace.ClrProfiler.Native.so")]
            public static extern bool IsProfilerAttached();
        }
    }
}
