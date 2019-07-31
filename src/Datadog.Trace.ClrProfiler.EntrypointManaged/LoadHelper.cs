using System;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.EntrypointManaged
{
    /// <summary>
    /// A class that attempts to load the Datadog.Trace.ClrProfiler.Managed .NET assembly.
    /// </summary>
    public static class LoadHelper
    {
        /// <summary>
        /// A method that attempts to load the Datadog.Trace.ClrProfiler.Managed .NET assembly.
        /// </summary>
        // /// <returns>A bool representing success/failure.</returns>
        public static void LoadManagedProfiler()
        {
            try
            {
                Assembly.Load(new AssemblyName("Datadog.Trace.ClrProfiler.Managed, Version=1.6.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb"));
            }
            catch
            {
            }
        }
    }
}
