using System;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Samples.AzureFunctions;

[assembly: FunctionsStartup(typeof(Startup))]
namespace Samples.AzureFunctions
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            Console.WriteLine("Startup method called.");
            // var profilerAttached = false;
            var profilerAttached = Datadog.Trace.ClrProfiler.Instrumentation.ProfilerAttached;
            if (profilerAttached)
            {
                Console.WriteLine("Profiler is attached.");
            }
        }
    }
}
