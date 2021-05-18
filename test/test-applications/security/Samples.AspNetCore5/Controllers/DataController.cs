using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Samples.WeblogAspNetCore31.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class DataController : Controller
    {
        [HttpPost]
        public IActionResult Index([FromBody]object body)
        {
            return Content("Received\n");
        }
    }
}
