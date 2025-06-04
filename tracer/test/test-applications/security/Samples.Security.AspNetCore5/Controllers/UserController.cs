#nullable enable
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Samples.Security.AspNetCore5.Controllers
{
    public class UserController : Controller
    {
        public IActionResult Index(string? userId = null)
        {
            SampleHelpers.SetUser(userId ?? "user3");
            return View();
        }

        public IActionResult TrackLoginSdk(bool success, string? userId = null)
        {
            var metadata = new Dictionary<string, string> { { "some-key", "some-value" } };
            var defaultUserId = "user-dog";
            if (success)
            {
                SampleHelpers.TrackUserLoginSuccessEvent(userId ?? defaultUserId, metadata);
            }
            else
            {
                SampleHelpers.TrackUserLoginFailureEvent(userId ?? defaultUserId, true, metadata);
            }

            return View("Index");
        }
    }
}
