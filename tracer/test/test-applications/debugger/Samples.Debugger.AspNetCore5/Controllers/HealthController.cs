using Microsoft.AspNetCore.Mvc;

namespace Samples.Debugger.AspNetCore5.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class HealthController : ControllerBase
    {
        public IActionResult Index()
        {
            return Content("Ok\n");
        }

        [HttpGet("params/{id}")]
        public IActionResult Params(string id)
        {
            return Content($"Hello {id}\n");
        }
    }
}
