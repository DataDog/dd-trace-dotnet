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

        // Check if we should test APIM proxy headers
        var testApim = Environment.GetEnvironmentVariable("DD_TEST_APIM_ENABLED");
        if (testApim == "1" || testApim?.ToLowerInvariant() == "true")
        {
            _logger.LogInformation("APIM test enabled, calling simple endpoint with APIM headers");
            await CallFunctionHttpWithProxy("simple");
        }
        else
        {
            await CallFunctionHttp("trigger");
        }

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
            _logger.LogInformation("Killing {PID} ({Name})", process.Id, process.ProcessName);
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
        var httpFunctionUrl = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME") ?? "localhost:7071";
        var uri = $"{$"http://{httpFunctionUrl}"}/api/{path}";
        _logger.LogInformation("Calling Uri {Uri}", uri);
        var simpleResponse = await HttpClient.GetStringAsync(uri);
        return simpleResponse;
    }

    private async Task<string> CallFunctionHttpWithProxy(string path)
    {
        var httpFunctionUrl = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME") ?? "localhost:7071";

        // TriggerAllTimer runs on host startup (RunOnStartup=true) and immediately calls back into
        // this same host. On a loaded CI agent the host/worker can be slow to start servicing
        // requests, so the self-call below would otherwise fail or time out and throw out of the
        // timer before _mutex.Set() is reached, hanging the whole run until ExitApp's 5 min mutex
        // wait expires. Wait until the host reports ready first, so the single (traced) APIM call
        // succeeds on the first attempt and we don't emit extra spans from failed attempts.
        await WaitForHostReady(httpFunctionUrl);

        var uri = $"http://{httpFunctionUrl}/api/{path}";
        _logger.LogInformation("Calling Uri with APIM headers: {Uri}", uri);

        var request = new HttpRequestMessage(HttpMethod.Get, uri);

        // Add Azure APIM proxy headers that will trigger the inferred span creation
        var startTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        request.Headers.Add("x-dd-proxy", "azure-apim");
        request.Headers.Add("x-dd-proxy-request-time-ms", startTimeMs.ToString());
        request.Headers.Add("x-dd-proxy-domain-name", "my-apim-instance.azure-api.net");
        request.Headers.Add("x-dd-proxy-httpmethod", "GET");
        request.Headers.Add("x-dd-proxy-path", $"/api/{path}");
        request.Headers.Add("x-dd-proxy-region", "canada central");

        var response = await HttpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("APIM proxy call completed with status: {StatusCode}", response.StatusCode);

        return content;
    }

    private async Task WaitForHostReady(string httpFunctionUrl)
    {
        // Requests to this admin status endpoint are filtered out by the test
        // (see the SpanFilters entry in AzureFunctionsTests.SubmitsTracesWithAzureApimHeaders), so
        // polling here does not produce spans that would affect the asserted span count.
        var statusUri = $"http://{httpFunctionUrl}/admin/host/status";
        var deadline = DateTime.UtcNow.AddSeconds(60);
        var delay = TimeSpan.FromMilliseconds(250);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var statusResponse = await HttpClient.GetAsync(statusUri, cts.Token);
                if (statusResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Function host reported ready ({StatusCode})", statusResponse.StatusCode);
                    return;
                }

                _logger.LogInformation("Function host not ready yet ({StatusCode}), retrying", statusResponse.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "Function host not reachable yet, retrying");
            }

            await Task.Delay(delay);
            if (delay < TimeSpan.FromSeconds(2))
            {
                delay += TimeSpan.FromMilliseconds(250);
            }
        }

        _logger.LogWarning("Timed out waiting for function host to become ready; attempting APIM call anyway");
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

