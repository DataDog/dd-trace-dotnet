using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Samples.AspNetCoreMvc.Controllers
{
    [Route("api")]
    public class ApiController : ControllerBase
    {
        private const string CorrelationIdentifierHeaderName = "sample.correlation.identifier";
        private readonly ILogger _logger;
        private Action<ILogger, string> _logMessage = (logger, page) => logger.LogInformation("Visited {Controller}/{Page} at {Time}", nameof(ApiController), page, DateTime.UtcNow.ToLongTimeString());

        public ApiController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        [Route("delay/{seconds}")]
        public ActionResult Delay(int seconds)
        {
            using var logScope = _logger.BeginScope(new DatadogLoggingScope());

            _logMessage(_logger, nameof(Delay));
            Thread.Sleep(TimeSpan.FromSeconds(seconds));
            AddCorrelationIdentifierToResponse();
            return Ok(seconds);
        }

        [HttpGet]
        [Route("delay-async/{seconds}")]
        public async Task<ActionResult> DelayAsync(int seconds)
        {
            using var logScope = _logger.BeginScope(new DatadogLoggingScope());

            _logMessage(_logger, nameof(DelayAsync));
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            AddCorrelationIdentifierToResponse();
            return Ok(seconds);
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
