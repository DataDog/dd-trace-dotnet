using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Samples.AspNetCoreMvc.Shared;

namespace Samples.AspNetCoreMvc.Controllers
{
    public class HomeController : Controller
    {
        private const string CorrelationIdentifierHeaderName = "sample.correlation.identifier";
        private readonly ILogger _logger;
        private Action<ILogger, string> _logMessage = (logger, page) => logger.LogInformation("Visited {Controller}/{Page} at {Time}", nameof(HomeController), page, DateTime.UtcNow.ToLongTimeString());

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            _logMessage(_logger, nameof(Index));
            var instrumentationType = Type.GetType("Datadog.Trace.ClrProfiler.Instrumentation, Datadog.Trace.ClrProfiler.Managed");
            ViewBag.ProfilerAttached = instrumentationType?.GetProperty("ProfilerAttached", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) ?? false;
            ViewBag.TracerAssemblyLocation = Type.GetType("Datadog.Trace.Tracer, Datadog.Trace")?.Assembly.Location;
            ViewBag.ClrProfilerAssemblyLocation = instrumentationType?.Assembly.Location;
            ViewBag.StackTrace = StackTraceHelper.GetUsefulStack();

            var prefixes = new[] { "COR_", "CORECLR_", "DD_", "DATADOG_" };

            var envVars = from envVar in Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
                          from prefix in prefixes
                          let key = (envVar.Key as string)?.ToUpperInvariant()
                          let value = envVar.Value as string
                          where key.StartsWith(prefix)
                          orderby key
                          select new KeyValuePair<string, string>(key, value);

            AddCorrelationIdentifierToResponse();
            return View(envVars.ToList());
        }

        [Route("delay/{seconds}")]
        public IActionResult Delay(int seconds)
        {
            _logMessage(_logger, nameof(Delay));
            ViewBag.StackTrace = StackTraceHelper.GetUsefulStack();
            Thread.Sleep(TimeSpan.FromSeconds(seconds));
            AddCorrelationIdentifierToResponse();
            return View(seconds);
        }

        [Route("delay-async/{seconds}")]
        public async Task<IActionResult> DelayAsync(int seconds)
        {
            _logMessage(_logger, nameof(DelayAsync));
            ViewBag.StackTrace = StackTraceHelper.GetUsefulStack();
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            AddCorrelationIdentifierToResponse();
            return View("Delay", seconds);
        }

        [Route("bad-request")]
        public IActionResult ThrowException()
        {
            _logMessage(_logger, nameof(ThrowException));
            AddCorrelationIdentifierToResponse();
            throw new Exception("This was a bad request.");
        }

        [Route("status-code/{statusCode}")]
        public string StatusCodeTest(int statusCode)
        {
            _logMessage(_logger, nameof(StatusCodeTest));
            AddCorrelationIdentifierToResponse();
            HttpContext.Response.StatusCode = statusCode;
            return $"Status code has been set to {statusCode}";
        }

        [Route("alive-check")]
        public string IsAlive()
        {
            _logMessage(_logger, nameof(IsAlive));
            AddCorrelationIdentifierToResponse();
            return "Yes";
        }

        private void AddCorrelationIdentifierToResponse()
        {
            if (Request.Headers.ContainsKey(CorrelationIdentifierHeaderName))
            {
                Response.Headers.Add(CorrelationIdentifierHeaderName, Request.Headers[CorrelationIdentifierHeaderName]);
            }
        }
    }
}
