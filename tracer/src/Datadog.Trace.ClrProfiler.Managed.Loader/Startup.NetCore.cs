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
        private static readonly Dictionary<string, Assembly> CachedAssemblies = new();
        private static CachedAssembly _fastPath = default;

        internal static System.Runtime.Loader.AssemblyLoadContext DependencyLoadContext { get; } = new ManagedProfilerAssemblyLoadContext();

        private static string ResolveManagedProfilerDirectory()
        {
            string tracerFrameworkDirectory = "netstandard2.0";

            var version = Environment.Version;

            // Old versions of .net core have a major version of 4
            if ((version.Major == 3 && version.Minor >= 1) || version.Major >= 5)
            {
                tracerFrameworkDirectory = "netcoreapp3.1";
            }

            var tracerHomeDirectory = ReadEnvironmentVariable("DD_DOTNET_TRACER_HOME") ?? string.Empty;
            return Path.Combine(tracerHomeDirectory, tracerFrameworkDirectory);
        }

        private static Assembly AssemblyResolve_ManagedProfilerDependencies(object sender, ResolveEventArgs args)
        {
            var fastPath = _fastPath;
            if (fastPath.Name == args.Name)
            {
                return fastPath.Assembly;
            }

            lock (CachedAssemblies)
            {
                if (CachedAssemblies.TryGetValue(args.Name, out Assembly assembly))
                {
                    return assembly;
                }

                var assemblyName = new AssemblyName(args.Name);

                // On .NET Framework, having a non-US locale can cause mscorlib
                // to enter the AssemblyResolve event when searching for resources
                // in its satellite assemblies. This seems to have been fixed in
                // .NET Core in the 2.0 servicing branch, so we should not see this
                // occur, but guard against it anyways. If we do see it, exit early
                // so we don't cause infinite recursion.
                if (string.Equals(assemblyName.Name, "System.Private.CoreLib.resources", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(assemblyName.Name, "System.Net.Http", StringComparison.OrdinalIgnoreCase))
                {
                    StartupLogger.Debug("Assembly {0} not found.", args.Name);
                    CachedAssemblies[args.Name] = null;
                    return null;
                }

                var path = Path.Combine(ManagedProfilerDirectory, $"{assemblyName.Name}.dll");

                // Only load the main profiler into the default Assembly Load Context.
                // If Datadog.Trace or other libraries are provided by the NuGet package their loads are handled in the following two ways.
                // 1) The AssemblyVersion is greater than or equal to the version used by Datadog.Trace, the assembly
                //    will load successfully and will not invoke this resolve event.
                // 2) The AssemblyVersion is lower than the version used by Datadog.Trace, the assembly will fail to load
                //    and invoke this resolve event. It must be loaded in a separate AssemblyLoadContext since the application will only
                //    load the originally referenced version
                if (File.Exists(path))
                {
                    StartupLogger.Debug("Loading {0} with DependencyLoadContext.LoadFromAssemblyPath", path);
                    assembly = DependencyLoadContext.LoadFromAssemblyPath(path); // Load unresolved framework and third-party dependencies into a custom Assembly Load Context
                    if (fastPath.Name == null)
                    {
                        _fastPath = new CachedAssembly(args.Name, assembly);
                    }
                    else
                    {
                        CachedAssemblies[args.Name] = assembly;
                    }

                    return assembly;
                }

                CachedAssemblies[args.Name] = null;
                return null;
            }
        }

        private readonly struct CachedAssembly
        {
            public readonly string Name;
            public readonly Assembly Assembly;

            public CachedAssembly(string name, Assembly assembly)
            {
                Name = name;
                Assembly = assembly;
            }
        }
    }
}

#endif
