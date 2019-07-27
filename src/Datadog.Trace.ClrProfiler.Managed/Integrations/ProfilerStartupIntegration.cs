using System.Reflection;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// ProfilerStartupIntegration provides an empty hook to call on startup of any profiled AppDomain
    /// </summary>
    public static class ProfilerStartupIntegration
    {
        private const string ReservedStartupKey = "_Datadog_Profiler_Startup_";
        private const string IntegrationName = "DatadogProfilerStartup";

        private static readonly ILog Log = LogProvider.GetLogger(typeof(ProfilerStartupIntegration));

        /// <summary>
        /// Method to insert within a try catch at the beginning of AppDomain code
        /// </summary>
        [InterceptMethod(
            TargetAssembly = ReservedStartupKey,
            TargetType = ReservedStartupKey,
            TargetSignatureTypes = new[] { ClrNames.Void })]
        public static void ForceLoad()
        {
            var datadogAssembly = typeof(ProfilerStartupIntegration).Assembly.FullName;
            var callingAssembly = Assembly.GetCallingAssembly().FullName;
            Log.Info($"Forcing eager loading of {datadogAssembly} from {callingAssembly}");
        }
    }
}
