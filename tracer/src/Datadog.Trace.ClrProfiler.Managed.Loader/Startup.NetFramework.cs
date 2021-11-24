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
            return Path.Combine(tracerHomeDirectory, "net461");
        }

        private static Assembly AssemblyResolve_ManagedProfilerDependencies(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name).Name;

            // On .NET Framework, having a non-US locale can cause mscorlib
            // to enter the AssemblyResolve event when searching for resources
            // in its satellite assemblies. Exit early so we don't cause
            // infinite recursion.
            if (string.Equals(assemblyName, "mscorlib.resources", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(assemblyName, "System.Net.Http", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var path = Path.Combine(ManagedProfilerDirectory, $"{assemblyName}.dll");

            if (File.Exists(path))
            {
                if (args.Name.StartsWith("Datadog.Trace, Version=") && args.Name != AssemblyName)
                {
                    StartupLogger.Debug("Trying to load {0} which does not match the expected version ({1})", args.Name, AssemblyName);
                    return null;
                }

                StartupLogger.Debug("Resolving {0}, loading {1}", args.Name, path);
                return Assembly.LoadFrom(path);
            }

            return null;
        }

        private static void TryLoadManagedAssembly()
        {
            try
            {
                var assembly = Assembly.Load(AssemblyName);

                if (assembly != null)
                {
                    // call method Datadog.Trace.ClrProfiler.Instrumentation.Initialize()
                    var type = assembly.GetType("Datadog.Trace.ClrProfiler.Instrumentation", throwOnError: false);
                    var method = type?.GetRuntimeMethod("Initialize", parameters: new Type[0]);
                    method?.Invoke(obj: null, parameters: null);
                }
            }
            catch (Exception ex)
            {
                StartupLogger.Log(ex, "Error when loading managed assemblies.");
            }
        }
    }
}

#endif
