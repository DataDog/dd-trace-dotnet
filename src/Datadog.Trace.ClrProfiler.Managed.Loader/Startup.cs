#define TRACE

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
            System.Diagnostics.Trace.WriteLine(string.Format("ManagedProfilerDirectory: {0}", ManagedProfilerDirectory));

            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve_ManagedProfilerDependencies;
            TryLoadManagedAssembly();
        }

        internal static string ManagedProfilerDirectory { get; }

        private static bool TryLoadManagedAssembly()
        {
            try
            {
                Assembly.Load(new AssemblyName("Datadog.Trace.ClrProfiler.Managed, Version=1.9.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb"));
                return true;
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine(string.Format("Exception in TryLoadManagedAssembly: {0}", e.Message));
                return false;
            }
        }
    }
}
