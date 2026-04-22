// <copyright file="ManagedProfilerAssemblyResolver.NetFramework.cs" company="Datadog">
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
    // This type owns the AppDomain.AssemblyResolve callback that the tracer
    // registers at startup on .NET Framework. It is intentionally a separate
    // static class from Startup so that invoking its static handler never
    // forces CLR type-initialization of Startup itself.
    //
    // Why that matters: if a configBuilder attached to <appSettings> (e.g.
    // AzureAppConfigurationBuilder with useAzureKeyVault and DefaultAzureCredential)
    // issues sync-over-async work during the Startup..cctor chain, the async
    // continuation can run on a ThreadPool thread that needs to resolve a type
    // (Type.GetType), which fires AppDomain.AssemblyResolve. If the handler
    // lives on Startup, that ThreadPool thread has to wait for Startup..cctor
    // to finish; the main thread is already blocked inside that .cctor waiting
    // for the Task, which is waiting for the ThreadPool thread -> classic
    // .cctor deadlock (APMS-19239).
    //
    // Keeping the handler on a class with a trivial .cctor means Startup..cctor
    // finishes the resolver's init before subscribing, so any ThreadPool thread
    // that later dispatches the handler sees the type as already initialized
    // and runs without blocking.
    internal static class ManagedProfilerAssemblyResolver
    {
        // Set by Startup..cctor before subscribing the handler below.
        // An auto-property with no initializer keeps this type beforefieldinit
        // and free of any meaningful class-init work.
        internal static string? ManagedProfilerDirectory { get; set; }

        internal static Assembly? OnAssemblyResolve(object sender, ResolveEventArgs args)
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

        internal static Assembly? ResolveAssembly(string name)
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
                if (name.StartsWith("Datadog.Trace, Version=", StringComparison.Ordinal) && name != Startup.AssemblyName)
                {
                    StartupLogger.Debug("  Trying to load '{0}' which does not match the expected version ('{1}'). [Path={2}]", name, Startup.AssemblyName, path);
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
