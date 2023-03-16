using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
    private static readonly ManualResetEventSlim _mutex = new(initialState: false, spinCount: 0);

    private readonly ILogger _logger;

    public AllTriggers(ILoggerFactory loggerFactory, IHostApplicationLifetime lifetime)
    {
        _lifetime = lifetime;
        _logger = loggerFactory.CreateLogger<AllTriggers>();
    }

    [Function("TriggerAllTimer")]
    public async Task TriggerAllTimer([TimerTrigger(AtMidnightOnFirstJan, RunOnStartup = true)] TimerInfo myTimer)
    {
        _logger.LogInformation($"Profiler attached: {SampleHelpers.IsProfilerAttached()}");
        _logger.LogInformation($"Profiler assembly location: {SampleHelpers.GetTracerAssemblyLocation()}");

        var envVars = string.Join(", ", SampleHelpers.GetDatadogEnvironmentVariables().Select(x => $"{x.Key}={x.Value}"));
        _logger.LogInformation("$Profiler env vars: {EnvVars}", envVars);

        _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        await CallFunctionHttp("trigger");
        _logger.LogInformation($"Trigger All Timer complete: {DateTime.Now}");
        _mutex.Set();
    }

    [Function("ExitApp")]
    public void ExitApp([TimerTrigger(AtMidnightOnFirstJan, RunOnStartup = true)] TimerInfo myTimer)
    {
        _logger.LogInformation($"Waiting for mutex");
        if (!_mutex.Wait(TimeSpan.FromMinutes(5)))
        {
            _logger.LogError($"Error waiting for mutex: not obtained after 5 minutes!");
        }

        _logger.LogInformation($"Pausing for 3s");
        Thread.Sleep(3_000); // just need time for the TriggerAllTimer to finish up etc

        _logger.LogInformation($"Calling StopApplication()");
        _lifetime.StopApplication();

        // brutally kill the host, as can't find any other way to signal it should stop
        foreach (var process in Process.GetProcessesByName("func"))
        {
            _logger.LogInformation("Killing {PID} ({Name}", process.Id, process.ProcessName);
            process.Kill();
        }
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
        await Attempt("simple", _logger);
        await Attempt("exception", _logger, expectFailure: true);
        await Attempt("error", _logger, expectFailure: true);
        await Attempt("badrequest", _logger, expectFailure: true);

        var res = req.CreateResponse(HttpStatusCode.OK);
        res.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        await res.WriteStringAsync("Attempting triggers");

        return res;
    }

    [Function("Exception")]
    public async Task<HttpResponseData> Exception(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "exception")] HttpRequestData req,
        ILogger log)
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
        var httpFunctionUrl = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME") ?? "localhost:7071";
        var uri = $"{$"http://{httpFunctionUrl}"}/api/{path}";
        _logger.LogInformation("Calling Uri {Uri}", uri);
        var simpleResponse = await HttpClient.GetStringAsync(uri);
        return simpleResponse;
    }

    private async Task Attempt(string endpoint, ILogger log, bool expectFailure = false)
    {
        try
        {
            await CallFunctionHttp(endpoint);
        }
        catch (Exception ex)
        {
            if (expectFailure)
            {
                log.LogInformation(ex, "Trigger attempt failure for {endpoint} as expected", endpoint);
            }
            else
            {
                log.LogError(ex, "Trigger attempt failure for {endpoint}", endpoint);
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

