using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Samples.AspNetCoreWebApi31.Controllers
{
    [ApiController]
    public class HomeController : ControllerBase
    {
        // GET alive-check
        [HttpGet]
        [Route("alive-check")]
        public string IsAlive()
        {
            return "Yes";
        }
    }
}
