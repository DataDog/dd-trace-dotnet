using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Net.Http;

namespace Samples.AspNetCoreSimpleController
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (Environment.GetEnvironmentVariable("COR_ENABLE_PROFILING") == "1" ||
                Environment.GetEnvironmentVariable("CORECLR_ENABLE_PROFILING") == "1")
            {
                Console.WriteLine(" Profiler path is: {0}", Environment.GetEnvironmentVariable("CORECLR_PROFILER_PATH"));

                bool isAttached = SampleHelpers.IsProfilerAttached();
                Console.WriteLine(" * Checking if the profiler is attached: {0}", isAttached);

                bool tracerEnabled = Environment.GetEnvironmentVariable("DD_TRACE_ENABLED") != "0";

                string ruleFile = Environment.GetEnvironmentVariable("DD_APPSEC_RULE");

                if (ruleFile != null)
                {
                    var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ruleFile);
                    var fullPathExists = File.Exists(fullPath);
                    Console.WriteLine($" * Using rule file: {fullPath}, exists: {fullPathExists}");
                }
                else
                {
                    Console.WriteLine($" * No rules file found");
                }

                if (!isAttached && tracerEnabled)
                {
                    Console.WriteLine("Error: Profiler is required and is not loaded.");
                    Environment.Exit(1);
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
    }
}
