#nullable enable
using System;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Datadog.Trace.AppSec;

namespace weblog
{
    [ApiController]
    [Route("sdk")]
    public class SdkController : Controller
    {
	    [HttpGet("login-success-v2")]
        public IActionResult LoginSuccessV2(string userLogin, string? userId)
        {
            EventTrackingSdkV2.TrackUserLoginSuccess(userLogin, userId, new Dictionary<string, string>
            {
                { "metadata01", "metadata01value" },
                { "metadata02", "metadata02value" }
            });
            return Content(HttpContext.Session.Id);
        }
        
        [HttpGet("login-success")]
        public IActionResult LoginSuccess(string? userId)
        {
            EventTrackingSdk.TrackUserLoginSuccessEvent(userId, new Dictionary<string, string>
            {
                { "metadata01", "metadata01value" },
                { "metadata02", "metadata02value" }
            });
            return Content(HttpContext.Session.Id);
        }

        [HttpGet("login-failure-v2")]
        public IActionResult LoginFailureV2(string userLogin, string? userId, bool? exists)
        {
            EventTrackingSdkV2.TrackUserLoginFailure(userLogin, exists ?? true, userId, new Dictionary<string, string>
            {
                { "metadata01", "metadata01value" },
                { "metadata02", "metadata02value" }
            });
            return Content(HttpContext.Session.Id);
        }
    }
}
