using System;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Contains static properties the provide instrumentation information.
    /// </summary>
    public static class Instrumentation
    {
        /// <summary>
        /// Gets the CLSID for the Datadog .NET profiler
        /// </summary>
        public static readonly string ProfilerClsid = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";

        static Instrumentation()
        {
            try
            {
                switch (Environment.OSVersion.Platform)
                {
                    case PlatformID.Win32NT:
                        ProfilerAttached = NativeMethods.Windows.IsProfilerAttached();
                        break;
                    case PlatformID.Unix:
                        ProfilerAttached = NativeMethods.Linux.IsProfilerAttached();
                        break;
                }
            }
            catch (DllNotFoundException)
            {
                ProfilerAttached = false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether Datadog's profiler is currently attached.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the profiler is currently attached; <c>false</c> otherwise.
        /// </value>
        public static bool ProfilerAttached { get; }
    }
}
