#if NETCOREAPP

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    /// <summary>
    /// See main description in <c>Startup.cs</c>
    /// </summary>
    public partial class Startup
    {
        // This is the list of all assemblies that are known to be OK for running Side-by-Side,
        // when different versions are referenced in the process.
        // List their simple name here. The maped value should always be True.
        // (It is used by the implementation to see if a load has already been attempted.)
        private readonly Dictionary<string, bool> _assembliesToLoadSxS = new Dictionary<string, bool>()
            {
                ["Datadog.Trace"] = true,
                // ["Add.Your.Assembly.Here"] = true
            };

        private bool ShouldLoadAssemblyIntoCustomContext(AssemblyName assemblyName, out string assemblyPath)
        {
            assemblyPath = null;

            if (assemblyName == null)
            {
                return false;
            }

            lock (_assembliesToLoadSxS)
            {
                if (_assembliesToLoadSxS.TryGetValue(assemblyName.Name, out bool shouldTryLoad))
                {
                    if (shouldTryLoad)
                    {
                        // We we should load SxS, but we have not tried yet. If the assmably even there?

                        if (TryFindAssemblyInProfilerDirectory(assemblyName, out assemblyPath))
                        {
                            // This method is called from AssemblyResolveEventHandler and whenever it returns true, we attempt the SxS load.
                            // Set the flag to not try again
                            _assembliesToLoadSxS[assemblyName.Name] = false;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private Assembly AssemblyResolveEventHandler(object sender, ResolveEventArgs args)
        {
            bool loadIntoCustomContext = false;
            string assemblyPath = null;

            // Is this an assembly we should try loading from the profiler directory?
            AssemblyName assemblyName = ParseAssemblyName(args?.Name);
            if (ShouldLoadAssemblyFromProfilerDirectory(assemblyName) && TryFindAssemblyInProfilerDirectory(assemblyName, out assemblyPath))
            {
                // Yes, then try loading it:
                try
                {
                    StartupLogger.Debug($"Assembly.LoadFrom(\"{assemblyPath}\")");
                    Assembly loadedAssembly = Assembly.LoadFrom(assemblyPath);

                    // If we loaded the assembly, then all is good. Return:
                    if (loadedAssembly != null)
                    {
                        return loadedAssembly;
                    }
                }
                catch
                {
                    // There was an error. Before giving it, see if we should try loading the assembly side by side.
                    // If so, we will attemt it below. Otherwise - error out.
                    loadIntoCustomContext = ShouldLoadAssemblyIntoCustomContext(assemblyName, out assemblyPath);
                    if (!loadIntoCustomContext)
                    {
                        throw;
                    }
                }
            }

            // We may or may not have just tried loading the assembly.
            // Regardless, it may be an assembly that should be loaded Side-by-Side.
            // If so, and we have not loaded it above, we will need to load it into a custom context;
            loadIntoCustomContext = loadIntoCustomContext || ShouldLoadAssemblyIntoCustomContext(assemblyName, out assemblyPath);

            if (loadIntoCustomContext)
            {
                StartupLogger.Debug($"ManagedProfilerAssemblyLoadContext.SingeltonInstance.LoadFromAssemblyPath({assemblyPath})");
                return ManagedProfilerAssemblyLoadContext.SingeltonInstance.LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }
    }
}

#endif
