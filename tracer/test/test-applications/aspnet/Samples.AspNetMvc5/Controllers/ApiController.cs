using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace Samples.AspNetMvc5.Controllers
{
    [RoutePrefix("api")]
    public class ApiController : System.Web.Http.ApiController
    {
        [HttpGet]
        [Route("delay/{seconds}")]
        public IHttpActionResult Delay(int seconds)
        {
            Thread.Sleep(TimeSpan.FromSeconds(seconds));
            return Json(seconds);
        }

        [HttpGet]
        [Route("delay-async/{seconds}")]
        public async Task<IHttpActionResult> DelayAsync(int seconds)
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds)).ConfigureAwait(false);
            return Json(seconds);
        }
        
        [HttpGet]
        [Route("delay-optional/{seconds?}")]
        public IHttpActionResult Optional(int? seconds = null)
        {
            Thread.Sleep(TimeSpan.FromSeconds(seconds ?? 0));
            return Json(seconds);
        }
        
        [HttpGet]
        [Route("~/api/absolute-route")]
        public IHttpActionResult AbsoluteRoute()
        {
            return Ok();
        }

        [HttpGet]
        [Route("environment")]
        public IHttpActionResult Environment()
        {
            return Json(System.Environment.GetEnvironmentVariables());
        }

        [HttpGet]
        [Route("statuscode/{value}")]
        public IHttpActionResult StatusCode(int value)
        {
            return this.Content((HttpStatusCode)value, "Status code set to " + value);
        }

        [HttpGet]
        [Route("transient-failure/{value}")]
        public IHttpActionResult TransientFailure(string value)
        {
            bool success = bool.TryParse(value, out bool result) && result;
            if (success)
            {
                return Json(System.Environment.GetEnvironmentVariables());
            }

            throw new ArgumentException($"Passed in value was not 'true': {value}");
        }
    }
}
