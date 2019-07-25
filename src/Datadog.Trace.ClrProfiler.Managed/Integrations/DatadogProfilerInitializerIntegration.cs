using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracer integration for DatadogProfilerInitializerIntegration.
    /// </summary>
    public static class DatadogProfilerInitializerIntegration
    {
        private const string DotNetFramework = ".NETFramework";
        private const string CoreFramework = ".NETCoreApp";
        private const string InitializationKey = "Datadog_Reserved_Startup_Method";
        private static readonly ILog Log = LogProvider.GetLogger(typeof(DatadogProfilerInitializerIntegration));

        /// <summary>
        /// Method to initialize all dependencies needed for the Datadog CLR profiler
        /// </summary>
        /// <param name="dependencies">Serialized dependency candidates</param>
        [InterceptMethod(
            TargetAssembly = InitializationKey,
            TargetType = InitializationKey,
            TargetSignatureTypes = new[] { ClrNames.Void })]
        public static void InitializeDatadogProfilerDependencies(byte[] dependencies)
        {
            try
            {
                Log.Debug($"Entering {nameof(InitializeDatadogProfilerDependencies)}");

                var RuntimeFrameworkDescription = RuntimeInformation.FrameworkDescription.ToLower();

                OSPlatform platform;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    platform = OSPlatform.Windows;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    platform = OSPlatform.Linux;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    platform = OSPlatform.OSX;
                }

                bool isCore = RuntimeFrameworkDescription.Contains("core");
                Architecture processArchitecture = RuntimeInformation.ProcessArchitecture;

                int major, minor, patch, build;

                TargetFrameworkAttribute targetFramework = Assembly.GetCallingAssembly().GetCustomAttribute<TargetFrameworkAttribute>();
                var parts = targetFramework.FrameworkName.Split(',');
                var runtime = parts[0];
                var isCoreClr = runtime.Equals(CoreFramework);

                var versionParts = parts[1].Replace("Version=v", string.Empty).Split('.');
                major = int.Parse(versionParts[0]);
                minor = int.Parse(versionParts[1]);

                if (versionParts.Length > 2)
                {
                    patch = int.Parse(versionParts[2]);
                }

                if (versionParts.Length > 3)
                {
                    build = int.Parse(versionParts[3]);
                }

                var dependencyCandidates = Serialize(dependencies);
                RuntimeType runtimeType = RuntimeType.Framework;

                if (isCoreClr)
                {
                    runtimeType = RuntimeType.Core;
                }

                var match = dependencyCandidates.FirstOrDefault(c => c.IsMatch(platform, runtimeType, major, minor));

                if (match != null)
                {
                    foreach (var matchAssembly in match.Assemblies)
                    {
                        Assembly.Load(matchAssembly);
                    }
                }
                else
                {
                    Log.Debug("We couldn't locate eligible dependencies for this version of .NET on this platform.");
                    // TODO: Do something to not profile
                }
            }
            catch (Exception ex)
            {
                Log.ErrorException("Error creating or populating scope.", ex);
            }
            finally
            {
                Log.Debug($"Exiting {nameof(InitializeDatadogProfilerDependencies)}");
            }
        }

        private static ProfilerDependencies[] Serialize(byte[] stuff)
        {
            return null;
        }
    }
}
