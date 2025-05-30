using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.MemoryStorage;

namespace Samples.Hangfire
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            GlobalConfiguration.Configuration
                               .UseMemoryStorage();

            using var server = new BackgroundJobServer();

            // Enqueue a simple job
            BackgroundJob.Enqueue(() => ExecuteTracedJob("from Main"));

            // Run additional jobs
            await Should_Create_Activity();
            await Should_Create_Activity_With_Status_Error_When_Job_Failed();

            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
        }

        public static void ExecuteTracedJob(string additionText)
        {
            Console.WriteLine("Hello from the Hangfire job! " + additionText);
        }

        public static async Task Should_Create_Activity()
        {
            var jobId = BackgroundJob.Enqueue<TestJob>(x => x.Execute());
            await WaitJobProcessedAsync(jobId, 5);
        }

        public static async Task Should_Create_Activity_With_Status_Error_When_Job_Failed()
        {
            var jobId = BackgroundJob.Enqueue<TestJob>(x => x.ThrowException());
            await WaitJobProcessedAsync(jobId, 5);
        }

        private static async Task WaitJobProcessedAsync(string jobId, int maxSeconds)
        {
            // Just simulate a wait for now
            await Task.Delay(1000 * maxSeconds);
        }
    }
    
}
