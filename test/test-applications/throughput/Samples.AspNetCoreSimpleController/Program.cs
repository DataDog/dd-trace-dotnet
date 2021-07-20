using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Reflection;

namespace Samples.AspNetCoreSimpleController
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (Environment.GetEnvironmentVariable("COR_ENABLE_PROFILING") == "1" ||
                Environment.GetEnvironmentVariable("CORECLR_ENABLE_PROFILING") == "1")
            {
                bool isAttached = IsProfilerAttached();
                Console.WriteLine(" * Checking if the profiler is attached: {0}", isAttached);
                if (!isAttached)
                {
                    Console.WriteLine("Error: Profiler is required and is not loaded.");
                    Environment.Exit(-1);
                    return;
                }
            }
            else
            {
                Console.WriteLine(" * Running without profiler.");
            }

            Console.WriteLine();

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });

        private static bool IsProfilerAttached()
        {
            var instrumentationType = Type.GetType("Datadog.Trace.ClrProfiler.Instrumentation", throwOnError: false);

            if (instrumentationType == null)
            {
                return false;
            }

            var property = instrumentationType.GetProperty("ProfilerAttached");

            var isAttached = property?.GetValue(null) as bool?;

            return isAttached ?? false;
        }
    }
}
