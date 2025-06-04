using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.AspNetCore.Routing;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace;

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
            if (sdk_user != null)
            {
                Samples.SampleHelpers.TrackUserLoginSuccessEvent(sdk_user, null);
            }

            return Content($"Hello, set the user to {sdk_user}");
        }
    }
}
