using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
                    Attempts = 1,
                    DelaysInSeconds = new[] { 5 },
                    OnAttemptsExceeded = AttemptsExceededAction.Fail
                });

            using var host = CreateHostBuilder().Build();
            await host.StartAsync();

            // Simulate MVC request triggering a job
            await Should_Create_Distributed_Trace();

            // Run other jobs
            await Should_Create_Span();
            await Should_Create_Span_With_Status_Error_When_Job_Failed();

            using var server = new BackgroundJobServer();
            Console.WriteLine("Hangfire Server started. Press Enter to exit...");
            Console.ReadLine();
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
            await Task.Delay(1000 * maxSeconds);
        }

        public static async Task Should_Create_Distributed_Trace()
        {
            using var client = new HttpClient();
            var response = await client.GetAsync("http://localhost:5000/trace");
            Console.WriteLine($"Response from MVC controller: {await response.Content.ReadAsStringAsync()}");
            await WaitJobProcessedAsync(1);
        }

        public static IHostBuilder CreateHostBuilder() =>
            Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls("http://localhost:5000");
                    webBuilder.ConfigureServices(services =>
                    {
                        services.AddHangfire(config => config.UseMemoryStorage());
                        services.AddHangfireServer();
                        services.AddControllers();
                    });

                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapControllers();
                        });
                    });
                });
    }

    public class TraceController : Microsoft.AspNetCore.Mvc.Controller
    {
        [HttpGet("/trace")]
        public IActionResult Trace()
        {
            BackgroundJob.Enqueue(() => Program.ExecuteTracedJob("from distributed trace"));
            return Content("Job enqueued");
        }
    }
}
