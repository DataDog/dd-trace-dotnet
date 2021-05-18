using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace AspNetCoreWebApp.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class RenderController : ControllerBase
    {
        [HttpGet("{str}")]
        public IActionResult Get(string str)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < 10000; i++)
            {
                sb.Append(str);
            }
            return Content(sb.ToString());
        }
    }
}
