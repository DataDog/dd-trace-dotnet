using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Samples.AzureFunctions.AllTriggers;

public class AllTriggers
{
    private readonly IHostApplicationLifetime _lifetime;
    private const string AtMidnightOnFirstJan = "0 0 0 1 Jan *";
    private static readonly HttpClient HttpClient = new();
    private static int _shutdownStarted;

    // Single source of truth for the function host base URL. Both the readiness wait and the self-calls
    // derive from this so they always target the same host and port.
    private static readonly string FunctionBaseUrl = $"http://{Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME") ?? "localhost:7071"}";

    private readonly ILogger _logger;

    public AllTriggers(ILogger<AllTriggers> logger, IHostApplicationLifetime lifetime)
    {
        _lifetime = lifetime;
        _logger = logger;
    }

    [Function("TriggerAllTimer")]
    public async Task TriggerAllTimer([TimerTrigger(AtMidnightOnFirstJan, RunOnStartup = true)] TimerInfo myTimer)
    {
        _logger.LogInformation($"Profiler attached: {SampleHelpers.IsProfilerAttached()}");
        _logger.LogInformation($"Profiler assembly location: {SampleHelpers.GetTracerAssemblyLocation()}");

        var envVars = string.Join(", ", SampleHelpers.GetDatadogEnvironmentVariables().Select(x => $"{x.Key}={x.Value}"));
        _logger.LogInformation("$Profiler env vars: {EnvVars}", envVars);

        _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

        // The RunOnStartup timer can fire before the Functions host has bound its port and registered
        // routes, so wait for readiness before making the (traced) self-call; otherwise it fails with
        // connection-refused (or a 404) and none of the downstream spans are produced.
        await SampleHelpers.WaitForFunctionHostReadyAsync(FunctionBaseUrl);
        await CallFunctionHttp("trigger");

        _logger.LogInformation($"Trigger All Timer complete: {DateTime.Now}");
    }

    [Function("Shutdown")]
    public HttpResponseData Shutdown(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "shutdown")] HttpRequestData req)
    {
        ScheduleShutdown();
        return req.CreateResponse(HttpStatusCode.Accepted);
    }

    [Function("SimpleHttpTrigger")]
    public async Task<HttpResponseData> SimpleHttpTrigger(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "simple")] HttpRequestData req)
    {
        await Task.Yield();
        using var s = SampleHelpers.CreateScope("Manual inside Simple");
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        var res = req.CreateResponse(HttpStatusCode.OK);
        res.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        await res.WriteStringAsync("This HTTP triggered function executed successfully!");

        return res;
    }

    [Function("TriggerCaller")]
    public async Task<HttpResponseData> Trigger(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "trigger")] HttpRequestData req)
    {
        using var s = SampleHelpers.CreateScope("Manual inside Trigger");

        _logger.LogInformation("Calling simple");
        await Attempt("simple");
        await Attempt("exception", expectFailure: true);
        await Attempt("error", expectFailure: true);
        await Attempt("badrequest", expectFailure: true);

        var res = req.CreateResponse(HttpStatusCode.OK);
        res.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        await res.WriteStringAsync("Attempting triggers");

        return res;
    }

    [Function("Exception")]
    public async Task<HttpResponseData> Exception(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "exception")] HttpRequestData req)
    {
        using var s = SampleHelpers.CreateScope("Manual inside Exception");

        await Task.Yield();

        _logger.LogInformation("Called exception HTTP trigger function.");

        throw new InvalidOperationException("Task failed successfully.");
    }

    [Function("ServerError")]
    public async Task<HttpResponseData> ServerError(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "error")] HttpRequestData req)
    {
        using var s = SampleHelpers.CreateScope("Manual inside ServerError");

        await Task.Yield();

        _logger.LogInformation("Called error HTTP trigger function.");

        return req.CreateResponse(HttpStatusCode.InternalServerError);
    }

    [Function("BadRequest")]
    public HttpResponseData BadRequest(
        [HttpTrigger(AuthorizationLevel.System, "get", "post", Route = "badrequest")] HttpRequestData req)
    {
        using var s = SampleHelpers.CreateScope("Manual inside BadRequest");

        _logger.LogInformation("Called badrequest HTTP trigger function.");

        return req.CreateResponse(HttpStatusCode.BadRequest);
    }

    private async Task<string> CallFunctionHttp(string path)
    {
        var uri = $"{FunctionBaseUrl}/api/{path}";
        _logger.LogInformation("Calling Uri {Uri}", uri);
        var simpleResponse = await HttpClient.GetStringAsync(uri);
        return simpleResponse;
    }

    private void ScheduleShutdown()
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            // Let the shutdown HTTP response complete before stopping the isolated worker. The test owns
            // the func.exe lifecycle and uses targeted process-tree cleanup if Core Tools remains running.
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            await SampleHelpers.ForceTracerFlushAsync();
            _logger.LogInformation("Stopping Azure Functions worker");
            _lifetime.StopApplication();
        });
    }

    private async Task Attempt(string endpoint, bool expectFailure = false)
    {
        try
        {
            await CallFunctionHttp(endpoint);
        }
        catch (Exception ex)
        {
            if (expectFailure)
            {
                _logger.LogInformation(ex, "Trigger attempt failure for {endpoint} as expected", endpoint);
            }
            else
            {
                _logger.LogError(ex, "Trigger attempt failure for {endpoint}", endpoint);
            }
        }
    }

    public class TimerInfo
    {
        public MyScheduleStatus ScheduleStatus { get; set; }

        public bool IsPastDue { get; set; }
    }

    public class MyScheduleStatus
    {
        public DateTime Last { get; set; }

        public DateTime Next { get; set; }

        public DateTime LastUpdated { get; set; }
    }

    public class SomeClass
    {
        public string Name { get; }

        public SomeClass(string name)
        {
            Name = name;
        }
    }
}

