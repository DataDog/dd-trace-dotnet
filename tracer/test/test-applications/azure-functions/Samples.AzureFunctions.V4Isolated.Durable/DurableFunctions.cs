using System.Diagnostics;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Samples;

namespace Samples.AzureFunctions.Durable;

public class DurableFunctions
{
    private static int _shutdownStarted;

    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<DurableFunctions> _logger;

    public DurableFunctions(ILogger<DurableFunctions> logger, IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _lifetime = lifetime;
    }

    [Function(nameof(StartDurableWorkflows))]
    public Task<HttpResponseData> StartDurableWorkflows(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "seed/durable")] HttpRequestData request,
        [DurableClient] DurableTaskClient client)
        => StartWorkflowsAsync(request, client);

    private async Task<HttpResponseData> StartWorkflowsAsync(HttpRequestData request, DurableTaskClient client)
    {
        try
        {
            string[] instanceIds =
            [
                await client.ScheduleNewOrchestrationInstanceAsync(nameof(DurableWorkflow)),
                await client.ScheduleNewOrchestrationInstanceAsync(nameof(FailingDurableWorkflow)),
            ];

            var response = request.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteStringAsync(string.Join(Environment.NewLine, instanceIds));
            return response;
        }
        finally
        {
            ScheduleShutdown();
        }
    }

    [Function(nameof(DurableWorkflow))]
    public static async Task<string> DurableWorkflow([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var greeting = await context.CallActivityAsync<string>(nameof(DurableActivity), "World");
        var entityId = new EntityInstanceId(nameof(DurableCounter), context.InstanceId);
        var count = await context.Entities.CallEntityAsync<int>(entityId, nameof(DurableCounter.Increment), 1);
        return $"{greeting} Entity count: {count}";
    }

    [Function(nameof(DurableActivity))]
    public static string DurableActivity([ActivityTrigger] string name)
    {
        using var scope = SampleHelpers.CreateScope("Manual inside DurableActivity");
        return $"Hello, {name}!";
    }

    [Function(nameof(FailingDurableWorkflow))]
    public static async Task<string> FailingDurableWorkflow([OrchestrationTrigger] TaskOrchestrationContext context)
        => await context.CallActivityAsync<string>(nameof(FailingDurableActivity), "World");

    [Function(nameof(FailingDurableActivity))]
    public static string FailingDurableActivity([ActivityTrigger] string name)
        => throw new InvalidOperationException($"Unable to greet {name}.");

    private void ScheduleShutdown()
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            await SampleHelpers.ForceTracerFlushAsync();
            _logger.LogInformation("Stopping Azure Functions host");
            _lifetime.StopApplication();

            foreach (var process in Process.GetProcessesByName("func"))
            {
                _logger.LogInformation("Killing {Pid} ({Name})", process.Id, process.ProcessName);
                process.Kill();
            }
        });
    }
}

public class DurableCounter : TaskEntity<int>
{
    public int Increment(int amount)
    {
        State += amount;
        return State;
    }

    [Function(nameof(DurableCounter))]
    public static Task Run([EntityTrigger] TaskEntityDispatcher dispatcher) => dispatcher.DispatchAsync<DurableCounter>();
}
