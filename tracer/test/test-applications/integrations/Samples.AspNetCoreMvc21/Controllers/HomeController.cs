using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Samples.AspNetCoreMvc.Shared;

namespace Samples.AspNetCoreMvc.Controllers
{
    public class HomeController : Controller
    {
        private const string CorrelationIdentifierHeaderName = "sample.correlation.identifier";

        public IActionResult Index()
        {
            ViewBag.ProfilerAttached = SampleHelpers.IsProfilerAttached();
            ViewBag.TracerAssemblyLocation = SampleHelpers.GetTracerAssemblyLocation();
            ViewBag.StackTrace = StackTraceHelper.GetUsefulStack();

            var envVars = SampleHelpers.GetDatadogEnvironmentVariables();

            AddCorrelationIdentifierToResponse();
            return View(envVars.ToList());
        }

        [Route("delay/{seconds}")]
        public IActionResult Delay(int seconds)
        {
            ViewBag.StackTrace = StackTraceHelper.GetUsefulStack();
            Thread.Sleep(TimeSpan.FromSeconds(seconds));
            AddCorrelationIdentifierToResponse();
            return View(seconds);
        }

        [Route("delay-async/{seconds}")]
        public async Task<IActionResult> DelayAsync(int seconds)
        {
            ViewBag.StackTrace = StackTraceHelper.GetUsefulStack();
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            AddCorrelationIdentifierToResponse();
            return View("Delay", seconds);
        }

        [Route("bad-request")]
        public IActionResult ThrowException()
        {
            AddCorrelationIdentifierToResponse();
            throw new Exception("This was a bad request.");
        }

        [Route("status-code/{statusCode}")]
        public string StatusCodeTest(int statusCode)
        {
            AddCorrelationIdentifierToResponse();
            HttpContext.Response.StatusCode = statusCode;
            return $"Status code has been set to {statusCode}";
        }

        [Route("alive-check")]
        public string IsAlive()
        {
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
