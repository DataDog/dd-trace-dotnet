using Microsoft.AspNetCore.Mvc;

namespace Samples.Security.AspNetCore5.Controllers;

[ApiController]
[Route("Status")]
public class StatusController : Controller
{
    [HttpGet("{status}")]
    public IActionResult IndexForm(string status)
    {
        var statusCode = int.Parse(status);
        HttpContext.Response.StatusCode = statusCode;

        return Content("Hello, World!\\n");
    }

    [HttpGet("{status}/body")]
    public IActionResult IndexFormWithBody(int status)
    {
        return StatusCode(status, new { message = "waf_sentinel_response_body" });
    }
}

