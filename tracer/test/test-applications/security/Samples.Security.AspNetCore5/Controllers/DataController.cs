using Microsoft.AspNetCore.Mvc;
using Samples.Security.AspNetCore5.Models;

namespace Samples.Security.AspNetCore5
{
    [Route("[controller]")]
    public class DataController : Controller
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
    }
}
