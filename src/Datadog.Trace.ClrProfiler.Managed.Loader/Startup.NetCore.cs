#if NETCOREAPP
using System;
using System.IO;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    /// <summary>
    /// A class that attempts to load the Datadog.Trace.ClrProfiler.Managed .NET assembly.
    /// </summary>
    public partial class Startup
    {
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
                return null;
            }

            var path = Path.Combine(ManagedProfilerDirectory, $"{assemblyName.Name}.dll");

            // Only load the main profiler into the default Assembly Load Context.
            // If Datadog.Trace or other libraries are provided by the NuGet package their loads are handled in the following two ways.
            // 1) The AssemblyVersion is greater than or equal to the version used by Datadog.Trace.ClrProfiler.Managed, the assembly
            //    will load successfully and will not invoke this resolve event.
            // 2) The AssemblyVersion is lower than the version used by Datadog.Trace.ClrProfiler.Managed, the assembly will fail to load
            //    and invoke this resolve event. It must be loaded in a separate AssemblyLoadContext since the application will only
            //    load the originally referenced version
            if (assemblyName.Name.StartsWith("Datadog.Trace.ClrProfiler.Managed", StringComparison.OrdinalIgnoreCase)
                && assemblyName.FullName.IndexOf("PublicKeyToken=def86d061d0d2eeb", StringComparison.OrdinalIgnoreCase) >= 0
                && File.Exists(path))
            {
                StartupLogger.Debug("Loading {0} with Assembly.LoadFrom", path);
                return Assembly.LoadFrom(path);
            }
            else if (File.Exists(path))
            {
                StartupLogger.Debug("Loading {0} with DependencyLoadContext.LoadFromAssemblyPath", path);
                return DependencyLoadContext.LoadFromAssemblyPath(path); // Load unresolved framework and third-party dependencies into a custom Assembly Load Context
            }

            return null;
        }
    }
}

#endif
