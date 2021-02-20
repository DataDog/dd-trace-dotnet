using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Datadog.AutoInstrumentation.ManagedLoader
{
    /// <summary>
    /// See main description in <c>AssemblyLoader.cs</c>
    /// </summary>
    internal partial class AssemblyResolveEventHandler
    {
        private const string LoggingComponentMoniker = AssemblyLoader.AssemblyLoggingComponentMonikerPrefix + nameof(AssemblyResolveEventHandler);

        // This handler will try to load assemblies from the folders specified in <c>ManagedProductBinariesDirectories</c>.
        // It will handle all assemblies with names that start with the prefixes below (<see cref="LoadAssemblyFromProductDirectoryPrefixes" />).
        // In addition, it will handle all assemblies with names specified in <c>AssemblyNamesToLoad</c>.
        private static readonly string[] LoadAssemblyFromProductDirectoryPrefixes = new string[]
        {
                        "Datadog.Trace",
                        "Datadog.AutoInstrumentation"
        };

        // The <c>AssembliesToLoadSxS</c> setting is only used in NetCore.
        // For some assemblies, if that assembly is referenced by the application, we still want to load a version from the product
        // directory in addition to (NOT instead of) the version loaded with the application. Such assemblies must be carefully
        // verified for their ability to run side-by-side in this manner. Only after such validation can they be white-listed here.
        // Note: If this list grows large, we should replace the list with a look-up set.
        private static readonly string[] AssembliesToLoadSxS = new string[]
        {
                        "Datadog.Trace",
                        // "Add.Your.Assembly.Here"
        };

        private readonly IReadOnlyList<string> _assemblyNamesToLoad;
        private readonly IReadOnlyList<string> _managedProductBinariesDirectories;

        public AssemblyResolveEventHandler(IReadOnlyList<string> assemblyNamesToLoad, IReadOnlyList<string> managedProductBinariesDirectories)
        {
            _assemblyNamesToLoad = (assemblyNamesToLoad) == null ? new string[0] : assemblyNamesToLoad;
            _managedProductBinariesDirectories = (managedProductBinariesDirectories) == null ? new string[0] : managedProductBinariesDirectories;
        }

        internal IReadOnlyList<string> AssemblyNamesToLoad
        {
            get { return _assemblyNamesToLoad; }
        }

        internal IReadOnlyList<string> ManagedProductBinariesDirectories
        {
            get { return _managedProductBinariesDirectories; }
        }

        private static AssemblyName ParseAssemblyName(string fullAssemblyName)
        {
            if (string.IsNullOrEmpty(fullAssemblyName))
            {
                return null;
            }

            try
            {
                var assemblyName = new AssemblyName(fullAssemblyName);
                return assemblyName;
            }
            catch
            {
                return null;
            }
        }

        private Assembly OnAssemblyResolveCore(AssemblyName assemblyName)
        {
            // Is this an assembly which should be loaded from the product directory?
            if (MustLoadAssemblyFromProductDirectory(assemblyName) && TryFindAssemblyInProductDirectory(assemblyName, out string assemblyPath))
            {
                // Yes, then try loading it:
                try
                {
                    Log.Debug(LoggingComponentMoniker, "Loading assembly from product directory", "assemblyName", assemblyName.FullName, "assemblyPath", assemblyPath);
                    Assembly loadedAssembly = Assembly.LoadFrom(assemblyPath);

                    if (loadedAssembly == null)
                    {
                        Log.Debug(LoggingComponentMoniker, "Not able to load assembly from product directory", "assemblyName", assemblyName.FullName, "assemblyPath", assemblyPath);
                    }

                    return loadedAssembly;
                }
                catch (Exception ex)
                {
                    Log.Error(LoggingComponentMoniker, "Error loading assembly from product directory", ex, "assemblyName", assemblyName.FullName, "assemblyPath", assemblyPath);
                    return null;
                }
            }

            return null;
        }

        private bool MustLoadAssemblyFromProductDirectory(AssemblyName assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName?.Name) || string.IsNullOrWhiteSpace(assemblyName?.FullName))
            {
                return false;
            }

            // The AssemblyResolveEventHandler will handle all assemblies that have specific prefixes:

            for (int i = 0; i < LoadAssemblyFromProductDirectoryPrefixes.Length; i++)
            {
                string prefix = LoadAssemblyFromProductDirectoryPrefixes[i];
                if (assemblyName.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // If an assembly does not have a white-listed prefix, but it has been specified as the actual startup assembly,
            // we will also handle it:

            for (int i = 0; i < _assemblyNamesToLoad.Count; i++)
            {
                if (assemblyName.FullName.Equals(_assemblyNamesToLoad[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryFindAssemblyInProductDirectory(AssemblyName assemblyName, out string fullPath)
        {
            string assemblyFileName = (assemblyName?.Name == null) ? null : $"{assemblyName.Name}.dll";

            if (assemblyFileName != null)
            {
                for (int i = 0; i < _managedProductBinariesDirectories.Count; i++)
                {
                    string productDir = _managedProductBinariesDirectories[i];
                    string pathInProductDir = Path.Combine(productDir, assemblyFileName).Trim();

                    if (File.Exists(pathInProductDir))
                    {
                        fullPath = pathInProductDir;
                        return true;
                    }
                }
            }

            fullPath = null;
            return false;
        }
    }
}
