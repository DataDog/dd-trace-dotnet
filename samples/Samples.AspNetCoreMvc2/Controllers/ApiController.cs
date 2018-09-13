using System;
using System.Threading;
using Microsoft.AspNetCore.Mvc;

namespace Samples.AspNetCoreMvc2.Controllers
{
    [Microsoft.AspNetCore.Mvc.ApiController]
    public class ApiController : ControllerBase
    {
        [HttpGet]
        [Route("api/delay/{seconds}")]
        public ActionResult<int> Delay(int seconds)
        {
            Thread.Sleep(TimeSpan.FromSeconds(seconds));
            return Ok(seconds);
        }
    }
}
