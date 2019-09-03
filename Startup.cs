using System;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    /// <summary>
    /// A class that attempts to load the Datadog.Trace.ClrProfiler.Managed .NET assembly.
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Initializes static members of the <see cref="Startup"/> class.
        /// This method also attempts to load the Datadog.Trace.ClrProfiler.Managed. NET assembly.
        /// </summary>
        static Startup()
        {
            LoadManagedAssembly();
        }

        internal static bool ManagedAssemblyFound { get; set; }

        private static void LoadManagedAssembly()
        {
            try
            {
                Assembly.Load(new AssemblyName("Datadog.Trace.ClrProfiler.Managed, Version=1.7.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb"));
                ManagedAssemblyFound = true;
            }
            catch
            {
                ManagedAssemblyFound = false;
            }
        }
    }
}
