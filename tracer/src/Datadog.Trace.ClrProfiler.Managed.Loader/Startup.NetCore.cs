// <copyright file="Startup.NetCore.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    /// <summary>
    /// A class that attempts to load the Datadog.Trace .NET assembly.
    /// </summary>
    public partial class Startup
    {
        private static readonly System.Runtime.Loader.AssemblyLoadContext DependencyLoadContext = new ManagedProfilerAssemblyLoadContext();

        private static CachedAssembly[]? _assemblies;

        internal static string ComputeTfmDirectory(string tracerHomeDirectory)
        {
            var version = Environment.Version;
            string managedLibrariesDirectory;

            if (version.Major >= 6)
            {
                // version > 6.0
                managedLibrariesDirectory = "net6.0";
            }
            else if (version is { Major: 3, Minor: >= 1 } || version.Major == 5)
            {
                // version is 3.1 or 5.0
                managedLibrariesDirectory = "netcoreapp3.1";
            }
            else
            {
                // version < 3.1 (note: previous versions of .NET Core had major version 4)
                managedLibrariesDirectory = "netstandard2.0";
            }

            var fullPath = Path.Combine(Path.GetFullPath(tracerHomeDirectory), managedLibrariesDirectory);

            if (Directory.Exists(fullPath))
            {
                // We use the List/Array approach due to the number of files in the tracer home folder (7 in netstandard, 2 netcoreapp3.1+)
                var assemblies = new List<CachedAssembly>();
                foreach (var file in Directory.EnumerateFiles(fullPath, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    assemblies.Add(new CachedAssembly(file, null));
                }

                _assemblies = [..assemblies];
                StartupLogger.Debug("Total number of assemblies: {0}", _assemblies.Length);
            }

            return fullPath;
        }

        internal static string GetProfilerPathEnvVarNameForArch()
        {
            return RuntimeInformation.ProcessArchitecture switch
                   {
                       Architecture.X64 => "CORECLR_PROFILER_PATH_64",
                       Architecture.X86 => "CORECLR_PROFILER_PATH_32",
                       Architecture.Arm64 => "CORECLR_PROFILER_PATH_ARM64",
                       Architecture.Arm => "CORECLR_PROFILER_PATH_ARM",
                       _ => throw new ArgumentOutOfRangeException(nameof(RuntimeInformation.ProcessArchitecture), RuntimeInformation.ProcessArchitecture, "Unsupported architecture")
                   };
        }

        internal static string GetProfilerPathEnvVarNameFallback()
        {
            return "CORECLR_PROFILER_PATH";
        }

        private static Assembly? AssemblyResolve_ManagedProfilerDependencies(object sender, ResolveEventArgs args)
        {
            return ResolveAssembly(args.Name);
        }

        private static Assembly? ResolveAssembly(string name)
        {
            var assemblyName = new AssemblyName(name);

            // On .NET Framework, having a non-US locale can cause mscorlib
            // to enter the AssemblyResolve event when searching for resources
            // in its satellite assemblies. This seems to have been fixed in
            // .NET Core in the 2.0 servicing branch, so we should not see this
            // occur but guard against it anyway. If we do see it, exit early
            // so we don't cause infinite recursion.
            if (string.Equals(assemblyName.Name, "System.Private.CoreLib.resources", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(assemblyName.Name, "System.Net.Http", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // WARNING: Logs must not be added _before_ we check for the above bail-out conditions
            StartupLogger.Debug("Assembly Resolve event received for: {0}. Searching in: {1}", name, ManagedProfilerDirectory);
            var path = Path.Combine(ManagedProfilerDirectory, $"{assemblyName.Name}.dll");

            if (IsDatadogAssembly(path, out var cachedAssembly))
            {
                // The file exists in the Home folder...
                if (cachedAssembly is not null)
                {
                    // The assembly is already loaded.
                    StartupLogger.Debug("Loading from cache. [Path: {0}]", path);
                    return cachedAssembly;
                }

                // Only load the main profiler into the default AssemblyLoadContext.
                // If the NuGet package provides Datadog.Trace or other libraries, loading them is handled in the following two ways:
                // 1) If the AssemblyVersion is greater than or equal to the version used by Datadog.Trace, the assembly
                //    will load successfully and will not invoke this resolve event.
                // 2) If the AssemblyVersion is lower than the version used by Datadog.Trace, the assembly will fail to load
                //    and invoke this resolve event. It must be loaded in a separate AssemblyLoadContext since the application will only
                //    load the originally referenced version.
                StartupLogger.Debug("Calling DependencyLoadContext.LoadFromAssemblyPath(\"{0}\")", path);
                var assembly = DependencyLoadContext.LoadFromAssemblyPath(path); // Load unresolved framework and third-party dependencies into a custom AssemblyLoadContext
                SetDatadogAssembly(path, assembly);
                return assembly;
            }

            // The file doesn't exist in the Home folder.
            StartupLogger.Debug("Assembly not found in path: {0}", path);
            return null;
        }

        private static bool IsDatadogAssembly(string path, out Assembly? cachedAssembly)
        {
            for (var i = 0; i < _assemblies!.Length; i++)
            {
                var assembly = _assemblies[i];
                if (assembly.Path == path)
                {
                    cachedAssembly = assembly.Assembly;
                    return true;
                }
            }

            cachedAssembly = null;
            return false;
        }

        private static void SetDatadogAssembly(string path, Assembly cachedAssembly)
        {
            for (var i = 0; i < _assemblies!.Length; i++)
            {
                if (_assemblies[i].Path == path)
                {
                    _assemblies[i] = new CachedAssembly(path, cachedAssembly);
                    return;
                }
            }
        }

        private readonly struct CachedAssembly
        {
            public readonly string Path;
            public readonly Assembly? Assembly;

            public CachedAssembly(string path, Assembly? assembly)
            {
                Path = path;
                Assembly = assembly;
            }
        }
    }
}

#endif
