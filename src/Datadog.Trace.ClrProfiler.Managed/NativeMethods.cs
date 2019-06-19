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

            return NonWindows.IsProfilerAttached();
        }

        // the "dll" extension is required on .NET Framework
        // and optional on .NET Core
        private static class Windows
        {
            [DllImport("Datadog.Trace.ClrProfiler.Native.dll")]
            public static extern bool IsProfilerAttached();
        }

        // assume .NET Core if not running on Windows
        private static class NonWindows
        {
            [DllImport("Datadog.Trace.ClrProfiler.Native")]
            public static extern bool IsProfilerAttached();
        }
    }
}
