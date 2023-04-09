using System.Linq;
using System.Web.Mvc;
using Datadog.Trace;
using Samples.AspNetMvc5.Models;

namespace Samples.AspNetMvc5.Controllers
{
    public class UserController : Controller
    {
        [ValidateInput(false)]
        public ActionResult Index()
        {
            var userId = "user3";

            var userDetails = new UserDetails()
            {
                Id = userId,
            };
            Tracer.Instance.ActiveScope?.Span.SetUser(userDetails);

            return View();
        }
    }
}
