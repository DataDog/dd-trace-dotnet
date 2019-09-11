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
            ManagedProfilerDirectory = ResolveManagedProfilerDirectory();
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve_ManagedProfilerDependencies;
            LoadManagedAssembly();
        }

        internal static bool ManagedAssemblyFound { get; set; }

        internal static string ManagedProfilerDirectory { get; }

#if NETCOREAPP
        internal static System.Runtime.Loader.AssemblyLoadContext DependencyLoadContext { get; } = new ManagedProfilerContext();
#endif

        private static string ResolveManagedProfilerDirectory()
        {
#if NETCOREAPP
            string tracerFrameworkDirectory = "netstandard2.0";
#else
            // We currently build two assemblies targeting .NET Framework.
            // If we're running on the .NET Framework, load the highest-compatible assembly
            string corlibFileVersionString = ((AssemblyFileVersionAttribute)typeof(object).Assembly.GetCustomAttribute(typeof(AssemblyFileVersionAttribute))).Version;
            string corlib461FileVersionString = "4.6.1055.0";

            // This will throw an exception if the version number does not match the expected 2-4 part version number of non-negative int32 numbers
            // Do we have any reason to believe that could happen?
            var corlibVersion = new Version(corlibFileVersionString);
            var corlib461Version = new Version(corlib461FileVersionString);
            var tracerFrameworkDirectory = corlibVersion < corlib461Version ? "net45" : "net461";
            // Console.WriteLine($"tracerFrameworkDirectory = {tracerFrameworkDirectory}"); // TODO REMOVE LOGGING
#endif

            var tracerHomeDirectory = Environment.GetEnvironmentVariable("DD_DOTNET_TRACER_HOME") ?? string.Empty;
            return Path.Combine(tracerHomeDirectory, tracerFrameworkDirectory);
        }

#if NETCOREAPP
        private static Assembly AssemblyResolve_ManagedProfilerDependencies(object sender, ResolveEventArgs args)
        {
            string assemblyName = new AssemblyName(args.Name).Name;
            var path = Path.Combine(ManagedProfilerDirectory, $"{assemblyName}.dll");

            // Console.WriteLine("-----------START ASSEMBLY RESOLVE EVENT-----------");
            // Console.WriteLine($"ResolveManagedProfiler: Attempting to load {args.Name} from {path}"); // TODO REMOVE LOGGING
            // Console.WriteLine(new System.Diagnostics.StackTrace());
            // Console.WriteLine("----------- END ASSEMBLY RESOLVE EVENT -----------");

            if (assemblyName.ToLower().Contains("datadog.trace") && File.Exists(path))
            {
                try
                {
                    // Console.WriteLine($"ResolveManagedProfiler: Attempting to load {args.Name} into the Default Load Context"); // TODO REMOVE LOGGING
                    return Assembly.LoadFrom(path); // Load the main profiler and tracer into the default Assembly Load Context
                }
                catch (Exception ex)
                {
                    // Console.WriteLine($"ResolveManagedProfiler: Error trying to load {args.Name} into the Default Load Context"); // TODO REMOVE LOGGING
                    // Console.WriteLine(ex);
                    return null;
                }
            }
            else if (File.Exists(path))
            {
                try
                {
                    // Console.WriteLine($"ResolveManagedProfiler: Attempting to load {args.Name} into the Profiler Load Context"); // TODO REMOVE LOGGING
                    return DependencyLoadContext.LoadFromAssemblyPath(path); // Load unresolved framework and third-party dependencies into a custom Assembly Load Context
                }
                catch (Exception ex)
                {
                    // Console.WriteLine($"ResolveManagedProfiler: Error trying to load {args.Name} into the Profiler Load Context"); // TODO REMOVE LOGGING
                    // Console.WriteLine(ex);
                    return null;
                }
            }

            return null;
        }
#else
        private static Assembly AssemblyResolve_ManagedProfilerDependencies(object sender, ResolveEventArgs args)
        {
            var path = Path.Combine(ManagedProfilerDirectory, $"{new AssemblyName(args.Name).Name}.dll");
            if (File.Exists(path))
            {
                return Assembly.LoadFrom(path);
            }

            return null;
        }
#endif

        private static void LoadManagedAssembly()
        {
            try
            {
                Assembly.Load(new AssemblyName("Datadog.Trace.ClrProfiler.Managed, Version=1.7.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb"));
                // Console.WriteLine($"ResolveManagedProfiler: SUCCESS, \"Datadog.Trace.ClrProfiler.Managed, Version = 1.7.0.0, Culture = neutral, PublicKeyToken = def86d061d0d2eeb\" was loaded."); // TODO REMOVE LOGGING
                ManagedAssemblyFound = true;
            }
            catch
            {
                // Console.WriteLine($"ResolveManagedProfiler: FAILURE, \"Datadog.Trace.ClrProfiler.Managed, Version = 1.7.0.0, Culture = neutral, PublicKeyToken = def86d061d0d2eeb\" was not loaded."); // TODO REMOVE LOGGING
                ManagedAssemblyFound = false;
            }
        }
    }
}
