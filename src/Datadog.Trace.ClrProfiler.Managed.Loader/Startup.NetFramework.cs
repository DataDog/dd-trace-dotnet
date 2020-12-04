#if NETFRAMEWORK

using System;
using System.IO;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    /// <summary>
    /// See main description in <c>Startup.cs</c>
    /// </summary>
    public partial class Startup
    {
        private Assembly AssemblyResolveEventHandler(object sender, ResolveEventArgs args)
        {
            AssemblyName assemblyName = ParseAssemblyName(args?.Name);
            if (ShouldLoadAssemblyFromProfilerDirectory(assemblyName) && TryFindAssemblyInProfilerDirectory(assemblyName, out string assemblyPath))
            {
                StartupLogger.Debug($"Assembly.LoadFrom(\"{assemblyPath}\")");
                return Assembly.LoadFrom(assemblyPath);
            }

            return null;
        }
    }
}

#endif
