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
                               .UseMemoryStorage()
                               .UseFilter(new AutomaticRetryAttribute
                                {
                                    Attempts = 1, // customize retry count
                                    OnAttemptsExceeded = AttemptsExceededAction.Fail // or Delete, depending on your needs
                                });

            // Enqueue a simple job
            BackgroundJob.Enqueue(() => ExecuteTracedJob("from Main"));
            
            // Run additional jobs
            await Should_Create_Span();
            await Should_Create_Span_With_Status_Error_When_Job_Failed();

            using (var server = new BackgroundJobServer())
            {
                Console.ReadLine();
            }
        }

        public static void ExecuteTracedJob(string additionText)
        {
            Console.WriteLine("Hello from the Hangfire job! " + additionText);
        }

        public static async Task Should_Create_Span()
        {
            var jobId = BackgroundJob.Enqueue<TestJob>(x => x.Execute());
            await WaitJobProcessedAsync(1);
        }
        
        public static async Task Should_Create_Span_With_Status_Error_When_Job_Failed()
        {
            var jobId = BackgroundJob.Enqueue<TestJob>(x => x.ThrowException());
            await WaitJobProcessedAsync(1);
        }

        private static async Task WaitJobProcessedAsync(int maxSeconds)
        {
            // Just simulate a wait for now
            await Task.Delay(1000 * maxSeconds);
        }
    }
    
}
