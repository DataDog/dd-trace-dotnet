using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Samples.AspNetCoreMvc.Controllers
{
    [Route("api")]
    public class ApiController : ControllerBase
    {
        private const string CorrelationIdentifierHeaderName = "sample.correlation.identifier";

        [HttpGet]
        [Route("delay/{seconds}")]
        public ActionResult Delay(int seconds)
        {
            Thread.Sleep(TimeSpan.FromSeconds(seconds));
            AddCorrelationIdentifierToResponse();
            return Ok(seconds);
        }

        [HttpGet]
        [Route("delay-async/{seconds}")]
        public async Task<ActionResult> DelayAsync(int seconds)
        {
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
