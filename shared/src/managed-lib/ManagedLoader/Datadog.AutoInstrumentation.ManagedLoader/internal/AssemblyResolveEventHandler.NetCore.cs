#if NETCOREAPP

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Datadog.AutoInstrumentation.ManagedLoader
{
    /// <summary>
    /// See main description in <c>AssemblyLoader.cs</c>
    /// </summary>
    internal partial class AssemblyResolveEventHandler
    {
        private readonly HashSet<string> _assembliesAttemptedToLoadSxS = new HashSet<string>();

        public Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            AssemblyName assemblyName = ParseAssemblyName(args?.Name);
            Assembly loadedAssembly = OnAssemblyResolveCore(assemblyName);

            // If the assembly loaded, we are done.
            if (loadedAssembly != null)
            {
                return loadedAssembly;
            }

            // We may or may not just have tried to load the assembly.
            // Regardless, we have not ended up loading it.
            // It may be an assembly that should be loaded Side-by-Side.
            // If so, we will need to load it into a custom context.

            if (MustLoadAssemblyIntoCustomContext(assemblyName, out string assemblyPath))
            {
                try
                {
                    Log.Debug(Constants.LoggingComponentMoniker, $"Assembly listed for SxS loading", "assemblyName", assemblyName.FullName, "assemblyPath", assemblyPath);
                    loadedAssembly = SxSAssemblyLoadContext.SingeltonInstance.LoadFromAssemblyPath(assemblyPath);

                    if (loadedAssembly == null)
                    {
                        Log.Debug(Constants.LoggingComponentMoniker, $"Not able to load assembly into SxS loading context", "assemblyName", assemblyName.FullName, "assemblyPath", assemblyPath);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(Constants.LoggingComponentMoniker, $"Error loading assembly into SxS loading context", ex, "assemblyName", assemblyName.FullName, "assemblyPath", assemblyPath);
                    loadedAssembly = null;
                }
            }

            return loadedAssembly;
        }

        private bool MustLoadAssemblyIntoCustomContext(AssemblyName assemblyName, out string assemblyPath)
        {
            if (assemblyName?.Name == null)
            {
                assemblyPath = null;
                return false;
            }

            // Look for the specified assembly in AssembliesToLoadSxS:
            // (Because AssembliesToLoadSxS is very short, it is an array/list. If it ever gets largem we need to use a look-up instead.)
            for (int i = 0; i < Constants.AssembliesToLoadSxS.Length; i++)
            {
                // Did we list the specified assembly for SxS loading?

                if (assemblyName.Name.Equals(Constants.AssembliesToLoadSxS[i], StringComparison.OrdinalIgnoreCase))
                {
                    lock (_assembliesAttemptedToLoadSxS)
                    {
                        // The specified assembly must be loaded SxS. DId we already try this?

                        if (_assembliesAttemptedToLoadSxS.Contains(assemblyName.Name))
                        {
                            // Already tried SxS loading. Do not try again.
                            assemblyPath = null;
                            return false;
                        }

                        // We need to load SxS, and we have not tried yet. Is the assmably even there?

                        if (TryFindAssemblyInProductDirectory(assemblyName, out assemblyPath))
                        {
                            // We found the assembly on disk, so we will return true to indicate that it needs to be loaded SxS.
                            // This method is called from AssemblyResolveEventHandler and whenever it returns true, SxS load to be atempted.
                            // Set the flag to not try again.
                            _assembliesAttemptedToLoadSxS.Add(assemblyName.Name);
                            return true;
                        }
                    }
                }
            }

            assemblyPath = null;
            return false;
        }
    }
}

#endif
