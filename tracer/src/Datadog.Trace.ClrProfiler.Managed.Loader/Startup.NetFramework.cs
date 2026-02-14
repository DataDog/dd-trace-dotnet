// <copyright file="Startup.NetFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System;
using System.IO;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    /// <summary>
    /// A class that attempts to load the Datadog.Trace .NET assembly.
    /// </summary>
    public partial class Startup
    {
        internal static string ComputeTfmDirectory(string tracerHomeDirectory)
        {
            return Path.Combine(Path.GetFullPath(tracerHomeDirectory), "net461");
        }

        internal static string GetProfilerPathEnvVarNameForArch()
        {
            return Environment.Is64BitProcess ? "COR_PROFILER_PATH_64" : "COR_PROFILER_PATH_32";
        }

        internal static string GetProfilerPathEnvVarNameFallback()
        {
            return "COR_PROFILER_PATH";
        }

        private static Assembly? AssemblyResolve_ManagedProfilerDependencies(object sender, ResolveEventArgs args)
        {
            try
            {
                return ResolveAssembly(args.Name);
            }
            catch (Exception ex)
            {
                StartupLogger.Log(ex, "Error resolving assembly: {0}", args.Name);
            }

            return null;
        }

        private static Assembly? ResolveAssembly(string name)
        {
            var assemblyName = new AssemblyName(name);

            // On .NET Framework, having a non-US locale can cause mscorlib
            // to enter the AssemblyResolve event when searching for resources
            // in its satellite assemblies. Exit early so we don't cause
            // infinite recursion.
            if (string.Equals(assemblyName.Name, "mscorlib.resources", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(assemblyName.Name, "System.Net.Http", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(assemblyName.Name, "vstest.console.resources", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // WARNING: Logs must not be added _before_ we check for the above bail-out conditions
            var path = string.IsNullOrEmpty(ManagedProfilerDirectory) ? $"{assemblyName.Name}.dll" : Path.Combine(ManagedProfilerDirectory, $"{assemblyName.Name}.dll");
            StartupLogger.Debug("Assembly Resolve event received for: {0}. Looking for: {1}", name, path);

            if (File.Exists(path))
            {
                if (name.StartsWith("Datadog.Trace, Version=", StringComparison.Ordinal) && name != AssemblyName)
                {
                    StartupLogger.Debug("  Trying to load '{0}' which does not match the expected version ('{1}'). [Path={2}]", name, AssemblyName, path);
                    return null;
                }

                StartupLogger.Debug("Calling Assembly.LoadFrom(\"{0}\")", path);
                var assembly = Assembly.LoadFrom(path);
                StartupLogger.Debug("Assembly loaded: {0}", assembly.FullName);
                return assembly;
            }

            StartupLogger.Debug("Assembly not found in path: {0}", path);
            return null;
        }
    }
}

#endif
