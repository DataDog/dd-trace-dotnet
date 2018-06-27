using System;
using System.Configuration;

// [assembly: System.Security.SecurityCritical]
// [assembly: System.Security.AllowPartiallyTrustedCallers]
namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Provides instrumentation probes that can be injected into profiled code.
    /// </summary>
    public static class Instrumentation
    {
        private static bool? _enabled;
        private static bool? _profilerAttached;

        /// <summary>
        /// Gets a value indicating whether tracing with Datadog's profiler is enabled.
        /// </summary>
        /// <value>
        ///   <c>true</c> if profiling is enabled; <c>false</c> otherwise.
        /// </value>
        public static bool Enabled
        {
            get
            {
                if (_enabled == null)
                {
                    string setting = ConfigurationManager.AppSettings["Datadog.Tracing:Enabled"];
                    _enabled = !string.Equals(setting, bool.FalseString, StringComparison.InvariantCultureIgnoreCase);
                }

                return _enabled.Value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether Datadog's profiler is currently attached.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the profiler is currentl attached; <c>false</c> otherwise.
        /// </value>
        public static bool ProfilerAttached
        {
            get
            {
                if (_profilerAttached == null)
                {
                    try
                    {
                        _profilerAttached = NativeMethods.IsProfilerAttached();
                    }
                    catch
                    {
                        _profilerAttached = false;
                    }
                }

                return _profilerAttached.Value;
            }
        }
    }
}
