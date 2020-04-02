using System;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    /// <summary>
    /// A class that attempts to load the Datadog.Trace.ClrProfiler.Managed .NET assembly.
    /// </summary>
    public partial class Startup
    {
        /// <summary>
        /// Initializes static members of the <see cref="Startup"/> class.
        /// This method also attempts to load the Datadog.Trace.ClrProfiler.Managed .NET assembly.
        /// </summary>
        static Startup()
        {
            ManagedProfilerDirectory = ResolveManagedProfilerDirectory();
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve_ManagedProfilerDependencies;
            TryLoadManagedAssembly();
        }

        internal static string ManagedProfilerDirectory { get; }

        private static void TryLoadManagedAssembly()
        {
            try
            {
                var assembly = Assembly.Load("Datadog.Trace.ClrProfiler.Managed, Version=1.16.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb");

                if (assembly != null)
                {
                    // call method Datadog.Trace.ClrProfiler.Instrumentation.Initialize()
                    var type = assembly.GetType("Datadog.Trace.ClrProfiler.Instrumentation", throwOnError: false);
                    var method = type?.GetRuntimeMethod("Initialize", parameters: new Type[0]);
                    method?.Invoke(obj: null, parameters: null);
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
