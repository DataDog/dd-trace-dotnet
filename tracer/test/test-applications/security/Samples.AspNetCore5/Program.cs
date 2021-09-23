using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Samples.AspNetCore5
{
    public class Program
    {
        public static void Main(string[] args)
        {
	    Console.WriteLine($"Profiler attached: {IsProfilerAttached()}");
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
            Type nativeMethodsType = Type.GetType("Datadog.Trace.ClrProfiler.NativeMethods, Datadog.Trace");
            MethodInfo profilerAttachedMethodInfo = nativeMethodsType.GetMethod("IsProfilerAttached");
            try
            {
                return (bool)profilerAttachedMethodInfo.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return false;
        }
    }
}
