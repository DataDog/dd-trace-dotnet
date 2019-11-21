#if NETCOREAPP
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
            var tracerHomeDirectory = Environment.GetEnvironmentVariable("DD_DOTNET_TRACER_HOME") ?? string.Empty;
            return Path.Combine(tracerHomeDirectory, tracerFrameworkDirectory);
        }

        private static Assembly AssemblyResolve_ManagedProfilerDependencies(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name);
            var path = Path.Combine(ManagedProfilerDirectory, $"{assemblyName.Name}.dll");

            if (assemblyName.Name.StartsWith("Datadog.Trace", StringComparison.OrdinalIgnoreCase)
                && assemblyName.FullName.IndexOf("PublicKeyToken=def86d061d0d2eeb", StringComparison.OrdinalIgnoreCase) >= 0
                && File.Exists(path))
            {
                // Load the main profiler and tracer into the default Assembly Load Context
                var assembly = Assembly.LoadFrom(path);

                if (assembly.GetName().Version == assemblyName.Version)
                {
                    // check that we loaded the exact version we are expecting
                    // (not a higher version, for example)
                    return assembly;
                }
            }
            else if (File.Exists(path))
            {
                return DependencyLoadContext.LoadFromAssemblyPath(path); // Load unresolved framework and third-party dependencies into a custom Assembly Load Context
            }

            return null;
        }
    }
}

#endif
