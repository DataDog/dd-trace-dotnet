using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace Samples.AspNetMvc5.Controllers
{
    public class ApiController : System.Web.Http.ApiController
    {
        [HttpGet]
        [Route("api/delay/{seconds}")]
        public IHttpActionResult Delay(int seconds)
        {
            Thread.Sleep(TimeSpan.FromSeconds(seconds));
            return Json(seconds);
        }

        [HttpGet]
        [Route("api/delay-async/{seconds}")]
        public async Task<IHttpActionResult> DelayAsync(int seconds)
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds)).ConfigureAwait(false);
            return Json(seconds);
        }

        [HttpGet]
        [Route("api/environment")]
        public IHttpActionResult Environment()
        {
            return Json(System.Environment.GetEnvironmentVariables());
        }
    }
}
