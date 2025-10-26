using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.States;
using Samples.Hangfire.Infrastructure;
using Samples.Hangfire.Jobs;

namespace Samples.Hangfire;

public class Program
{
    private static readonly ActivitySource AdditionalActivitySource = new("AdditionalActivitySource");
    public static async Task Main(string[] args)
    {
        GlobalConfiguration.Configuration
                           .UseSimpleAssemblyNameTypeSerializer()
                           .UseRecommendedSerializerSettings()
                           .UseMemoryStorage();

        GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute
        {
            Attempts = 0,
            OnAttemptsExceeded = AttemptsExceededAction.Fail,
            LogEvents = false
        });
        GlobalJobFilters.Filters.Add(new JobCompletionFilter());

        Console.WriteLine("Starting Hangfire server...");
        using var server = new BackgroundJobServer();

        try
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("service.name", "OTEL-parent"),
            };
            using var localActivity = AdditionalActivitySource.StartActivity(
                name: "OtelParent",
                kind: ActivityKind.Internal,
                tags: tags
            );
            // run tests
            await Should_Create_Span();
            await Should_Create_Span_With_Status_Error_When_Job_Failed();
        }
        finally
        {
            Console.WriteLine("All jobs done. Stopping server...");
            // disposing will wait for workers to drain
        }
    }

    private static async Task Should_Create_Span()
    {
        var jobId = BackgroundJob.Enqueue<TestJob>(x => x.Execute());

        var result = await JobCompletion.Register(jobId, new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token);

        Console.WriteLine($"Job {result.JobId} completed successfully = {result.Succeeded}");
    }

    private static async Task Should_Create_Span_With_Status_Error_When_Job_Failed()
    {
        var jobId = BackgroundJob.Enqueue<TestJob>(x => x.ThrowException());

        try
        {
            await JobCompletion.Register(jobId, new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Expected failure observed for job {jobId}: {ex.Message}");
        }
    }
}
