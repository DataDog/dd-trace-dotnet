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
    public class VoidController : ControllerBase
    {
        [HttpGet]
        public void Get()
        {
        }
    }
}
