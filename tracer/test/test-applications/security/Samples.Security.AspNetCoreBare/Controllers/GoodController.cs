using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Samples.Security.AspNetCoreBare.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class GoodController : ControllerBase
    {
        [HttpGet]
        public string[] Get()
        {
            return new[] { "hello", "world" };
        }
    }
}
