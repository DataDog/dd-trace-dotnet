using Microsoft.AspNetCore.Mvc;

namespace Samples.Security.AspNetCore5.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class HealthController : ControllerBase
    {
        public IActionResult Index()
        {
            return Content("Ok\n");
        }

        [HttpGet("params/{str}")]
        public IActionResult Params(string str)
        {
            return Content($"Hello {str}\n");
        }
    }
}
