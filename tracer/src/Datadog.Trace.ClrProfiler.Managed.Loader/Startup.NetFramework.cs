// <copyright file="Startup.NetFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

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
        private static string ResolveManagedProfilerDirectory()
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

        private static Assembly AssemblyResolve_ManagedProfilerDependencies(object sender, ResolveEventArgs args)
        {
            return ResolveAssembly(args.Name);
        }

        private static Assembly ResolveAssembly(string name)
        {
            var assemblyName = new AssemblyName(name);

            // On .NET Framework, having a non-US locale can cause mscorlib
            // to enter the AssemblyResolve event when searching for resources
            // in its satellite assemblies. Exit early so we don't cause
            // infinite recursion.
            if (string.Equals(assemblyName.Name, "mscorlib.resources", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(assemblyName.Name, "System.Net.Http", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var appDomainFriendlyName = AppDomain.CurrentDomain.FriendlyName;

            // WARNING: Logs must not be added _before_ we check for the above bail-out conditions
            StartupLogger.Debug("[AppDomain={0}] Assembly Resolve event received for: {1}", appDomainFriendlyName, name);
            var path = Path.Combine(ManagedProfilerDirectory, $"{assemblyName.Name}.dll");
            StartupLogger.Debug("[AppDomain={0}] Looking for: {1}", appDomainFriendlyName, path);

            if (File.Exists(path))
            {
                if (name.StartsWith("Datadog.Trace, Version=") && name != AssemblyName)
                {
                    StartupLogger.Debug("[AppDomain={0}] Trying to load {1} which does not match the expected version ({2}). [Path={3}]", appDomainFriendlyName, name, AssemblyName, path);
                    return null;
                }

                StartupLogger.Debug("[AppDomain={0}] Resolving {1}, loading {2}", appDomainFriendlyName, name, path);
                return Assembly.LoadFrom(path);
            }

            StartupLogger.Debug("[AppDomain={0}] Assembly not found in path: {1}", appDomainFriendlyName, path);
            return null;
        }
    }
}

#endif
