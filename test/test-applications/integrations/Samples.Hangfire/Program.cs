using OpenTelemetry.Resources;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Hangfire;
using System;
using Hangfire.MemoryStorage;

namespace Samples.Hangfire
{
    internal class Program
    {
        static void Main(string[] args)
        {
            using var tracer = Sdk
    .CreateTracerProviderBuilder()
        .AddHangfireInstrumentation()
    .AddConsoleExporter()
    .Build();

            GlobalConfiguration.Configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseMemoryStorage();

            BackgroundJob.Enqueue(() => Console.WriteLine("Hello, world!"));

            using (var server = new BackgroundJobServer())
            {
                Console.ReadLine();
            }

        }
    }
}