using System;
using System.IO;
using System.Reflection;

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
            AppDomain.CurrentDomain.AssemblyResolve += ResolveManagedProfiler;
            LoadManagedAssembly();
        }

        internal static bool ManagedAssemblyFound { get; set; }

        private static Assembly ResolveManagedProfiler(object sender, ResolveEventArgs args)
        {
            string tracerDirectory = Environment.GetEnvironmentVariable("DD_DOTNET_TRACER_HOME");

            if (!string.IsNullOrEmpty(tracerDirectory))
            {
#if NETSTANDARD2_0
                string tracerFrameworkDirectory = "netstandard2.0";
#else
                string tracerFrameworkDirectory = "net45";
#endif
                var path = Path.Combine(tracerDirectory, tracerFrameworkDirectory, $"{new AssemblyName(args.Name).Name}.dll");
                if (File.Exists(path))
                {
                    // TODO REMOVE LOGGING
                    Console.WriteLine($"ResolveManagedProfiler: Attempting to load {args.Name} from {path}");
                    return Assembly.LoadFrom(path);
                }
            }

            return null;
        }

        private static void LoadManagedAssembly()
        {
            try
            {
                Assembly.Load(new AssemblyName("Datadog.Trace.ClrProfiler.Managed, Version=1.7.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb"));
                // TODO REMOVE LOGGING
                Console.WriteLine($"ResolveManagedProfiler: SUCCESS, \"Datadog.Trace.ClrProfiler.Managed, Version = 1.7.0.0, Culture = neutral, PublicKeyToken = def86d061d0d2eeb\" was loaded.");
                ManagedAssemblyFound = true;
            }
            catch
            {
                // TODO REMOVE LOGGING
                Console.WriteLine($"ResolveManagedProfiler: FAILURE, \"Datadog.Trace.ClrProfiler.Managed, Version = 1.7.0.0, Culture = neutral, PublicKeyToken = def86d061d0d2eeb\" was not loaded.");
                ManagedAssemblyFound = false;
            }
        }
    }
}
