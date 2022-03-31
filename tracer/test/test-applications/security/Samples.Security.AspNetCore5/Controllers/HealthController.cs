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
    }
}
