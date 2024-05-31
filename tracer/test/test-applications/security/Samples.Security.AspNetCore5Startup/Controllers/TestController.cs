using Microsoft.AspNetCore.Mvc;

namespace Samples.Security.AspNetCore5Startup.Controllers;

[ApiController]
[Route("[controller]")]
public class TestController : Controller
{
    [HttpGet("VerbTampering")]
    [HttpPut("VerbTampering")]
    public IActionResult VerbTampering()
    {
        return Content("VerbTampering Test");
    }
    
    public IActionResult Index()
    {
        return Content("Ok\n");
    }
}
