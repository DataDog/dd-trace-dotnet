using System;
using System.Configuration;
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
        private static readonly Lazy<bool> _profilerAttached = new Lazy<bool>(
            () =>
            {
                try
                {
                    return NativeMethods.IsProfilerAttached();
                }
                catch
                {
                    return false;
                }
            },
            LazyThreadSafetyMode.PublicationOnly);

        /// <summary>
        /// Gets a value indicating whether Datadog's profiler is currently attached.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the profiler is currentl attached; <c>false</c> otherwise.
        /// </value>
        public static bool ProfilerAttached => _profilerAttached.Value;
    }
}
