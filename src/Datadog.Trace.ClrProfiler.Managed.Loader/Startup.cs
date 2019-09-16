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
        /// This method also attempts to load the Datadog.Trace.ClrProfiler.Managed. NET assembly.
        /// </summary>
        static Startup()
        {
            ManagedProfilerDirectory = ResolveManagedProfilerDirectory();
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve_ManagedProfilerDependencies;
            TryLoadManagedAssembly();
        }

        internal static string ManagedProfilerDirectory { get; }

        private static bool TryLoadManagedAssembly()
        {
            try
            {
                var assemblyName = Assembly.GetExecutingAssembly().GetName();
                assemblyName.Name = "Datadog.Trace.ClrProfiler.Managed";

                Assembly.Load(assemblyName);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
