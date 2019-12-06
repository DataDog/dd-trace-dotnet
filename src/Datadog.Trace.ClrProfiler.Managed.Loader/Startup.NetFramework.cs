#if NETFRAMEWORK

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
        private static string ResolveManagedProfilerDirectory()
        {
            // We currently build two assemblies targeting .NET Framework.
            // If we're running on the .NET Framework, load the highest-compatible assembly
            string corlibFileVersionString = ((AssemblyFileVersionAttribute)typeof(object).Assembly.GetCustomAttribute(typeof(AssemblyFileVersionAttribute))).Version;
            string corlib461FileVersionString = "4.6.1055.0";

            // This will throw an exception if the version number does not match the expected 2-4 part version number of non-negative int32 numbers,
            // but mscorlib should be versioned correctly
            var corlibVersion = new Version(corlibFileVersionString);
            var corlib461Version = new Version(corlib461FileVersionString);
            var tracerFrameworkDirectory = corlibVersion < corlib461Version ? "net45" : "net461";

            Console.WriteLine($"ResolveManagedProfilerDirectory: DD_DOTNET_TRACER_HOME={Environment.GetEnvironmentVariable("DD_DOTNET_TRACER_HOME")}");
            Console.WriteLine($"ResolveManagedProfilerDirectory: DD_INTEGRATIONS={Environment.GetEnvironmentVariable("DD_INTEGRATIONS")}");
            var tracerHomeDirectory = Environment.GetEnvironmentVariable("DD_DOTNET_TRACER_HOME") ?? Path.GetDirectoryName(Environment.GetEnvironmentVariable("DD_INTEGRATIONS")) ?? string.Empty;
            Console.WriteLine($"ResolveManagedProfilerDirectory: returning {Path.Combine(tracerHomeDirectory, tracerFrameworkDirectory)}");
            return Path.Combine(tracerHomeDirectory, tracerFrameworkDirectory);
        }

        private static Assembly AssemblyResolve_ManagedProfilerDependencies(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name).Name;

            // On .NET Framework, having a non-US locale can cause mscorlib
            // to enter the AssemblyResolve event when searching for resources
            // in its satellite assemblies. Exit early so we don't cause
            // infinite recursion.
            if (string.Equals(assemblyName, "mscorlib.resources", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var path = Path.Combine(ManagedProfilerDirectory, $"{assemblyName}.dll");
            if (File.Exists(path))
            {
                return Assembly.LoadFrom(path);
            }

            return null;
        }
    }
}

#endif
