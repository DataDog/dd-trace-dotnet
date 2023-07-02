using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Samples.AzureFunctions.AllTriggers
{
    public class AllTriggers
    {
        private const string AtMidnightOnFirstJan = "0 0 0 1 Jan *";
        private static readonly HttpClient HttpClient = new();

        [FunctionName("TriggerAllTimer")]
        public async Task TriggerAllTimer([TimerTrigger(AtMidnightOnFirstJan, RunOnStartup = true)] TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"Profiler attached: {SampleHelpers.IsProfilerAttached()}");
            log.LogInformation($"Profiler assembly location: {SampleHelpers.GetTracerAssemblyLocation()}");

            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            await CallFunctionHttp("trigger", log);
            log.LogInformation($"Shutting down: {DateTime.Now}");
        }

        [FunctionName("ExitApp")]
        public async Task ExitApp([TimerTrigger(AtMidnightOnFirstJan, RunOnStartup = true)] TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"Pausing for 30s");
            await Task.Delay(30_000);
            log.LogInformation($"Calling Environment.Exit");
            Environment.Exit(0);
        }

        [FunctionName("SimpleHttpTrigger")]
        public async Task<IActionResult> SimpleHttpTrigger(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "simple")] HttpRequest req,
            ILogger log)
        {
            await Task.Yield();
            using var s = SampleHelpers.CreateScope("Manual inside Simple");
            log.LogInformation("C# HTTP trigger function processed a request.");
            return new OkObjectResult("This HTTP triggered function executed successfully. ");
        }

        [FunctionName("Exception")]
        public async Task<IActionResult> Exception(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "exception")] HttpRequest req,
            ILogger log)
        {
            using var s = SampleHelpers.CreateScope("Manual inside Exception");

            await Task.Yield();

            log.LogInformation("Called error HTTP trigger function.");

            throw new InvalidOperationException("Task failed successfully.");
        }

        [FunctionName("ServerError")]
        public async Task<IActionResult> ServerError(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "error")] HttpRequest req,
            ILogger log)
        {
            using var s = SampleHelpers.CreateScope("Manual inside ServerError");

            await Task.Yield();

            log.LogInformation("Called error HTTP trigger function.");

            return new InternalServerErrorResult();
        }

        [FunctionName("BadRequest")]
        public IActionResult BadRequest(
            [HttpTrigger(AuthorizationLevel.System, "get", "post", Route = "badrequest")] HttpRequest req,
            ILogger log)
        {
            using var s = SampleHelpers.CreateScope("Manual inside BadRequest");

            log.LogInformation("Called badrequest HTTP trigger function.");

            return new BadRequestResult();
        }

        [FunctionName("TriggerCaller")]
        public async Task<IActionResult> Trigger(
                [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "trigger")] HttpRequest req,
                ILogger log)
        {
            using var s = SampleHelpers.CreateScope("Manual inside Trigger");
            string triggersText = req.Query["types"];
            var triggers = triggersText?.ToLower()?.Split(",");
            var doAll = triggers == null || triggers.Length == 0 || triggers.Contains("all");

            if (doAll || triggers.Contains("http"))
            {
                await Attempt(() => CallFunctionHttp("simple", log), log);
                await Attempt(() => CallFunctionHttp("exception", log), log, expectFailure: true);
                await Attempt(() => CallFunctionHttp("error", log), log, expectFailure: true);
                await Attempt(() => CallFunctionHttp("badrequest", log), log, expectFailure: true);
            }

            return new OkObjectResult("Attempting triggers.");
        }

        private async Task<HttpResponseMessage> CallFunctionHttp(string path, ILogger logger)
        {
            var httpFunctionUrl = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME") ?? "localhost:7071";
            var uri = $"{$"http://{httpFunctionUrl}"}/api/{path}";
            logger.LogInformation("Calling Uri {Uri}", uri);
            var response = await HttpClient.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                logger.LogError($"http call to uri: {uri}, failed with status-code: {response.StatusCode}, body: {content}");
                // calling this creates an exception with very little info about why the service failed
                response.EnsureSuccessStatusCode();
            }
            return response;
        }

        private async Task Attempt(Func<Task> action, ILogger log, bool expectFailure = false)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                if (expectFailure)
                {
                    log.LogInformation(ex, "Trigger attempt failure as expected");
                }
                else
                {
                    log.LogError(ex, "Trigger attempt failure");
                }
            }
        }
    }
}
