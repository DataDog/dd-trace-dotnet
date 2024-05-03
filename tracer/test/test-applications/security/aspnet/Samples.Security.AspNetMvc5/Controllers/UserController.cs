using System.Linq;
using System.Web.Mvc;
using Samples.AspNetMvc5.Models;

namespace Samples.AspNetMvc5.Controllers
{
    public class UserController : Controller
    {
        [ValidateInput(false)]
        public ActionResult Index()
        {
            Samples.SampleHelpers.SetUser("user3");

            return View();
        }
    }
}
