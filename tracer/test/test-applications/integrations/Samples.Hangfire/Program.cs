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
        var isBaggageScenario = args.Length > 0 && args[0] == "baggage";
        using var server = CreateServer(isBaggageScenario);

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

            if (isBaggageScenario)
            {
                await Should_Not_Accumulate_Baggage_Across_Jobs();
                return;
            }

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

    private static BackgroundJobServer CreateServer(bool suppressExecutionContext)
    {
        if (!suppressExecutionContext)
        {
            return new BackgroundJobServer();
        }

        // Keep the worker's mutable AsyncLocal baggage instance isolated from the in-process producers.
        using (ExecutionContext.SuppressFlow())
        {
            return new BackgroundJobServer(new BackgroundJobServerOptions { WorkerCount = 1 });
        }
    }

    private static async Task Should_Not_Accumulate_Baggage_Across_Jobs()
    {
        await Run_Baggage_Job("job-one", "one");
        await Run_Baggage_Job("job-two", "two");
    }

    private static async Task Run_Baggage_Job(string baggageKey, string baggageValue)
    {
        // Model an independent producer request, with its own execution context and baggage instance.
        Task<string> enqueueTask;
        using (ExecutionContext.SuppressFlow())
        {
            enqueueTask = Task.Run(() =>
            {
                using var activity = AdditionalActivitySource.StartActivity($"Enqueue {baggageKey}");
                OpenTelemetry.Baggage.Current = OpenTelemetry.Baggage.Create(new Dictionary<string, string>
                {
                    [baggageKey] = baggageValue,
                });

                return BackgroundJob.Enqueue<TestJob>(x => x.PrintBaggage(baggageKey));
            });
        }

        var jobId = await enqueueTask;
        var result = await JobCompletion.Register(jobId, new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token);

        Console.WriteLine($"Job {result.JobId} completed successfully = {result.Succeeded}");
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
