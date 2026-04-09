using Microsoft.AspNetCore.Mvc;

namespace weblog
{
    [ApiController]
    [Route("session")]
    public class SessionController : Controller
    {
	    [HttpGet("new")]
        public IActionResult New()
        {
            return Content(HttpContext.Session.Id);
        }

        [HttpGet("user")]
        public new IActionResult User(string sdk_user)
        {
            return Content($"Hello, set the user to {sdk_user}");
        }
    }
}
