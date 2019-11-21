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
                var assemblyName = new AssemblyName("Datadog.Trace.ClrProfiler.Managed, Version=1.9.1.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb");
                var assembly = Assembly.Load(assemblyName);

                // check that we loaded the exact assembly we are expecting
                // (not a higher version, for example)
                return assembly.GetName() == assemblyName;
            }
            catch
            {
                return false;
            }
        }
    }
}
