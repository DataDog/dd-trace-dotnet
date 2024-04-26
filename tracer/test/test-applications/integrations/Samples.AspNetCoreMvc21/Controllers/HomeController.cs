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

        [HttpGet]
        public IActionResult Index()
        {
            ViewBag.ProfilerAttached = SampleHelpers.IsProfilerAttached();
            ViewBag.TracerAssemblyLocation = SampleHelpers.GetTracerAssemblyLocation();
            ViewBag.StackTrace = StackTraceHelper.GetUsefulStack();

            var envVars = SampleHelpers.GetDatadogEnvironmentVariables();

            AddCorrelationIdentifierToResponse();
            return View(envVars.ToList());
        }

        [HttpGet("delay/{seconds}")]
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

        [Route("status-code-string/{statusCode}")]
        public string StatusCodeTestString(string input)
        {
            AddCorrelationIdentifierToResponse();
            if (int.TryParse(input, out int statusCode))
            {
                HttpContext.Response.StatusCode = statusCode;
                return $"Status code has been set to {statusCode}";
            }
            else
            {
                throw new Exception("Input was not a status code");
            }
        }

        [Route("handled-exception")]
        public IActionResult HandledException(string input)
        {
            AddCorrelationIdentifierToResponse();
            try
            {
                throw new Exception("Exception thrown and caught");
            }
            catch (Exception ex)
            {
                SampleHelpers.TrySetExceptionOnActiveScope(ex);
                return StatusCode(500, new { user_message = "There was an error, returning 500: " + ex.Message });
            }
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
                Response.Headers[CorrelationIdentifierHeaderName] = Request.Headers[CorrelationIdentifierHeaderName];
            }
        }
    }
}
