using Datadog.Trace;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Samples.Security.AspNetCore5.Models;

namespace Samples.Security.AspNetCore5
{
    [Route("[controller]")]
    [ApiController]
    public class MetaStructController : ControllerBase
    {

        [HttpGet("MetaStructTest")]
        public IActionResult MetaStructTest()
        {
            Tracer.AddMetaStructData();         
            return Content($"test Launched\n");
        }
    }
}
