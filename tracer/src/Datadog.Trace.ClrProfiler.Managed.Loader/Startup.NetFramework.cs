// <copyright file="Startup.NetFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.IO;
using System.Reflection;

#nullable enable

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    /// <summary>
    /// A class that attempts to load the Datadog.Trace .NET assembly.
    /// </summary>
    public partial class Startup
    {
        private static string? ResolveManagedProfilerDirectory()
        {
            var tracerHomeDirectory = ReadEnvironmentVariable("DD_DOTNET_TRACER_HOME") ?? string.Empty;
            var fullPath = Path.Combine(tracerHomeDirectory, "net461");
            if (!Directory.Exists(fullPath))
            {
                StartupLogger.Log($"The tracer home directory cannot be found at '{fullPath}', based on the DD_DOTNET_TRACER_HOME value '{tracerHomeDirectory}'");
                return null;
            }

            return fullPath;
        }

        private static Assembly? AssemblyResolve_ManagedProfilerDependencies(object sender, ResolveEventArgs args)
        {
            return ResolveAssembly(args.Name);
        }

        private static Assembly? ResolveAssembly(string name)
        {
            var assemblyName = new AssemblyName(name);
            StartupLogger.Debug("Assembly Resolve event received for: {0}", name);

            // On .NET Framework, having a non-US locale can cause mscorlib
            // to enter the AssemblyResolve event when searching for resources
            // in its satellite assemblies. Exit early so we don't cause
            // infinite recursion.
            if (string.Equals(assemblyName.Name, "mscorlib.resources", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(assemblyName.Name, "System.Net.Http", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var path = Path.Combine(ManagedProfilerDirectory, $"{assemblyName.Name}.dll");
            StartupLogger.Debug("Looking for: {0}", path);

            if (File.Exists(path))
            {
                if (name.StartsWith("Datadog.Trace, Version=") && name != AssemblyName)
                {
                    StartupLogger.Debug("Trying to load {0} which does not match the expected version ({1}). [Path={2}]", name, AssemblyName, path);
                    return null;
                }

                StartupLogger.Debug("Resolving {0}, loading {1}", name, path);
                return Assembly.LoadFrom(path);
            }

            StartupLogger.Debug("Assembly not found in path: {0}", path);
            return null;
        }
    }
}

#endif
