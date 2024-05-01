// <copyright file="Startup.NetCore.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    /// <summary>
    /// A class that attempts to load the Datadog.Trace .NET assembly.
    /// </summary>
    public partial class Startup
    {
        private static CachedAssembly[] _assemblies;

        internal static System.Runtime.Loader.AssemblyLoadContext DependencyLoadContext { get; } = new ManagedProfilerAssemblyLoadContext();

        private static string ResolveManagedProfilerDirectory()
        {
            string tracerFrameworkDirectory = "netstandard2.0";

            var version = Environment.Version;

            // Old versions of .net core have a major version of 4
            if ((version.Major == 3 && version.Minor >= 1) || version.Major >= 5)
            {
                tracerFrameworkDirectory = version.Major >= 6 ? "net6.0" : "netcoreapp3.1";
            }

            var tracerHomeDirectory = ReadEnvironmentVariable("DD_DOTNET_TRACER_HOME") ?? string.Empty;
            var fullPath = Path.Combine(tracerHomeDirectory, tracerFrameworkDirectory);

            if (!Directory.Exists(fullPath))
            {
                StartupLogger.Log($"The tracer home directory cannot be found at '{fullPath}', based on the DD_DOTNET_TRACER_HOME value '{tracerHomeDirectory}'");
                return null;
            }

            // We use the List/Array approach due the number of files in the tracer home folder (7 in netstandard, 2 netcoreapp3.1+)
            var assemblies = new List<CachedAssembly>();
            foreach (var file in Directory.EnumerateFiles(fullPath, "*.dll", SearchOption.TopDirectoryOnly))
            {
                assemblies.Add(new CachedAssembly(file, null));
            }

            _assemblies = assemblies.ToArray();
            StartupLogger.Debug("Total number of assemblies: {0}", _assemblies.Length);

            return fullPath;
        }

        private static Assembly AssemblyResolve_ManagedProfilerDependencies(object sender, ResolveEventArgs args)
        {
            return ResolveAssembly(args.Name);
        }

        private static Assembly ResolveAssembly(string name)
        {
            var assemblyName = new AssemblyName(name);

            // On .NET Framework, having a non-US locale can cause mscorlib
            // to enter the AssemblyResolve event when searching for resources
            // in its satellite assemblies. This seems to have been fixed in
            // .NET Core in the 2.0 servicing branch, so we should not see this
            // occur, but guard against it anyways. If we do see it, exit early
            // so we don't cause infinite recursion.
            if (string.Equals(assemblyName.Name, "System.Private.CoreLib.resources", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(assemblyName.Name, "System.Net.Http", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // WARNING: Logs must not be added _before_ we check for the above bail-out conditions
            StartupLogger.Debug("Assembly Resolve event received for: {0}", name);
            var path = Path.Combine(ManagedProfilerDirectory, $"{assemblyName.Name}.dll");
            StartupLogger.Debug("Looking for: {0}", path);

            if (IsDatadogAssembly(path, out var cachedAssembly))
            {
                // The file exists in the Home folder...
                if (cachedAssembly is not null)
                {
                    // The assembly is already loaded.
                    StartupLogger.Debug("Loading from cache. [Path: {0}]", path);
                    return cachedAssembly;
                }

                // Only load the main profiler into the default Assembly Load Context.
                // If Datadog.Trace or other libraries are provided by the NuGet package their loads are handled in the following two ways.
                // 1) The AssemblyVersion is greater than or equal to the version used by Datadog.Trace, the assembly
                //    will load successfully and will not invoke this resolve event.
                // 2) The AssemblyVersion is lower than the version used by Datadog.Trace, the assembly will fail to load
                //    and invoke this resolve event. It must be loaded in a separate AssemblyLoadContext since the application will only
                //    load the originally referenced version
                StartupLogger.Debug("Loading {0} with DependencyLoadContext.LoadFromAssemblyPath", path);
                var assembly = DependencyLoadContext.LoadFromAssemblyPath(path); // Load unresolved framework and third-party dependencies into a custom Assembly Load Context
                SetDatadogAssembly(path, assembly);
                return assembly;
            }

            // The file doesn't exist in the Home folder.
            StartupLogger.Debug("Assembly not found in path: {0}", path);
            return null;
        }

        private static bool IsDatadogAssembly(string path, out Assembly cachedAssembly)
        {
            for (var i = 0; i < _assemblies.Length; i++)
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
            for (var i = 0; i < _assemblies.Length; i++)
            {
                if (_assemblies[i].Path == path)
                {
                    _assemblies[i] = new CachedAssembly(path, cachedAssembly);
                    break;
                }
            }
        }

        private readonly struct CachedAssembly
        {
            public readonly string Path;
            public readonly Assembly Assembly;

            public CachedAssembly(string path, Assembly assembly)
            {
                Path = path;
                Assembly = assembly;
            }
        }
    }
}

#endif
