using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace Samples.Owin.WebApi.Controllers
{
    [RoutePrefix("")]
    public class HomeController : ApiController
    {
        [Route("")]
        [HttpGet]
        public IHttpActionResult Index()
        {
            return Ok();
        }

        [Route("api/delay/{seconds}")]
        [HttpGet]
        public IHttpActionResult ApiDelay(int seconds)
        {
            Thread.Sleep(TimeSpan.FromSeconds(seconds));
            return Ok($"ApiDelay {seconds} seconds");
        }

        [Route("delay/{seconds}")]
        [HttpGet]
        public IHttpActionResult Delay(int seconds)
        {
            Thread.Sleep(TimeSpan.FromSeconds(seconds));
            return Ok($"Delay {seconds} seconds");
        }

        [Route("delay-async/{seconds}")]
        [HttpGet]
        public async Task<IHttpActionResult> DelayAsync(int seconds)
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            return Ok($"DelayAsync {seconds} seconds");
        }

        [Route("bad-request")]
        [HttpGet]
        public void ThrowException()
        {
            throw new Exception("This was a bad request.");
        }

        [Route("status-code/{statusCode}")]
        [HttpGet]
        public IHttpActionResult StatusCodeTest(int statusCode)
        {
            return StatusCode((System.Net.HttpStatusCode)statusCode);
        }

        [Route("alive-check")]
        [HttpGet]
        public string IsAlive()
        {
            return "Yes";
        }
    }
}
