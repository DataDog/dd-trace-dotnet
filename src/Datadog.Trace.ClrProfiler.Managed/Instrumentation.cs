using System;
using System.Threading;

// [assembly: System.Security.SecurityCritical]
// [assembly: System.Security.AllowPartiallyTrustedCallers]
namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Provides instrumentation probes that can be injected into profiled code.
    /// </summary>
    public static class Instrumentation
    {
        /// <summary>
        /// Gets the CLSID for the Datadog .NET profiler
        /// </summary>
        public static readonly string ProfilerClsid = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";

        private static readonly Lazy<bool> _profilerAttached = new Lazy<bool>(
            () =>
            {
                try
                {
                    return NativeMethods.IsProfilerAttached();
                }
                catch (DllNotFoundException)
                {
                    return false;
                }
            },
            LazyThreadSafetyMode.PublicationOnly);

        /// <summary>
        /// Gets a value indicating whether Datadog's profiler is currently attached.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the profiler is currently attached; <c>false</c> otherwise.
        /// </value>
        public static bool ProfilerAttached => _profilerAttached.Value;
    }
}
