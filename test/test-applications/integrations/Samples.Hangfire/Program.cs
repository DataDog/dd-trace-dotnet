using System;
using System.Diagnostics;
using Hangfire;
using Hangfire.MemoryStorage;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Samples.Hangfire
{
    internal class Program
    {
        // Define a shared ActivitySource for tracing
        private static readonly ActivitySource ActivitySource = new("Samples.Hangfire");

        static void Main(string[] args)
        {
            // Set up OpenTelemetry
            using var tracer = Sdk.CreateTracerProviderBuilder()
                                  .AddHangfireInstrumentation()
                                  .AddConsoleExporter()
                                  .Build();

            // Configure Hangfire
            GlobalConfiguration.Configuration
                               .UseMemoryStorage()
                               .UseSimpleAssemblyNameTypeSerializer()
                               .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                               .UseSimpleAssemblyNameTypeSerializer()
                               .UseRecommendedSerializerSettings();

            // Start Hangfire server

            // Create a span for enqueuing the job

            BackgroundJob.Enqueue(() => ExecuteTracedJob());
            BackgroundJob.Schedule(() => ExecuteTracedJob(), TimeSpan.FromSeconds(10));
            using (var server = new BackgroundJobServer())
            {
                Console.ReadLine();
            }

        }

        public static void ExecuteTracedJob()
        {
            using var activity = ActivitySource.StartActivity("ExecuteTracedJob.Function");
            Console.WriteLine("Hello from the Hangfire job!");
            System.Threading.Thread.Sleep(1000); // Simulate work
        }
    }
}
