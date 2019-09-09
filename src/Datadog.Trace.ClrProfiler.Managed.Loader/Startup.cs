using System;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    /// <summary>
    /// A class that attempts to load the Datadog.Trace.ClrProfiler.Managed .NET assembly.
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Initializes static members of the <see cref="Startup"/> class.
        /// This method also attempts to load the Datadog.Trace.ClrProfiler.Managed. NET assembly.
        /// </summary>
        static Startup()
        {
#if NETFRAMEWORK
            // We currently build two assemblies targeting .NET Framework.
            // If we're running on the .NET Framework, load the highest-compatible assembly
            string corlibFileVersionString = ((AssemblyFileVersionAttribute)typeof(object).Assembly.GetCustomAttribute(typeof(AssemblyFileVersionAttribute))).Version;
            string corlib461FileVersionString = "4.6.1055.0";

            // This will throw an exception if the version number does not match the expected 2-4 part version number of non-negative int32 numbers
            // Do we have any reason to believe that could happen?
            var corlibVersion = new Version(corlibFileVersionString);
            var corlib461Version = new Version(corlib461FileVersionString);
            var tracerFrameworkDirectory = corlibVersion < corlib461Version ? "net45" : "net461";
            Console.WriteLine($"tracerFrameworkDirectory = {tracerFrameworkDirectory}"); // TODO REMOVE LOGGING
#else
            string tracerFrameworkDirectory = "netstandard2.0";
#endif

            var tracerHomeDirectory = Environment.GetEnvironmentVariable("DD_DOTNET_TRACER_HOME") ?? string.Empty;
            ManagedProfilerDirectory = Path.Combine(tracerHomeDirectory, tracerFrameworkDirectory);
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve_ManagedProfilerDependencies;
            LoadManagedAssembly();
        }

        internal static bool ManagedAssemblyFound { get; set; }

        internal static string ManagedProfilerDirectory { get; }

        private static Assembly AssemblyResolve_ManagedProfilerDependencies(object sender, ResolveEventArgs args)
        {
            var path = Path.Combine(ManagedProfilerDirectory, $"{new AssemblyName(args.Name).Name}.dll");
            if (File.Exists(path))
            {
                Console.WriteLine($"ResolveManagedProfiler: Attempting to load {args.Name} from {path}"); // TODO REMOVE LOGGING
                return Assembly.LoadFrom(path);
            }

            return null;
        }

        private static void LoadManagedAssembly()
        {
            try
            {
                Assembly.Load(new AssemblyName("Datadog.Trace.ClrProfiler.Managed, Version=1.7.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb"));
                Console.WriteLine($"ResolveManagedProfiler: SUCCESS, \"Datadog.Trace.ClrProfiler.Managed, Version = 1.7.0.0, Culture = neutral, PublicKeyToken = def86d061d0d2eeb\" was loaded."); // TODO REMOVE LOGGING
                ManagedAssemblyFound = true;
            }
            catch
            {
                Console.WriteLine($"ResolveManagedProfiler: FAILURE, \"Datadog.Trace.ClrProfiler.Managed, Version = 1.7.0.0, Culture = neutral, PublicKeyToken = def86d061d0d2eeb\" was not loaded."); // TODO REMOVE LOGGING
                ManagedAssemblyFound = false;
            }
        }
    }
}
