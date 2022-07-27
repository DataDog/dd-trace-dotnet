using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Samples.Security.AspNetCore5.Models;

namespace Samples.Security.AspNetCore5
{
    [Route("[controller]")]
    [ApiController]
    public class DataApiController : Controller
    {
        [HttpPost]
        public IActionResult Index([FromBody]object body)
        {
            return Content("Received\n");
        }

        [Route("model")]
        public IActionResult Model(MyModel model)
        {
            return Content($"Received model with properties: {model}");
        }

        [Route("array")]
        public IActionResult Array(IEnumerable<string> model)
        {
            return Content($"Received array : {string.Join(',', model)}");
        }

        [Route("complex-model")]
        public IActionResult Model(ComplexModel model)
        {
            return Content($"Received model with properties: {model}");
        }
    }
}
