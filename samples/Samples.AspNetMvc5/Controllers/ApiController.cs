using System;
using System.Threading;
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
            return Ok(seconds);
        }

        [HttpGet]
        [Route("api/environment")]
        public IHttpActionResult Environment()
        {            
            return Ok(System.Environment.GetEnvironmentVariables());
        }
    }
}
