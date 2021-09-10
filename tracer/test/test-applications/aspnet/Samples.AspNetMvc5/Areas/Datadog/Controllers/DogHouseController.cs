using System.Web.Mvc;

namespace Samples.AspNetMvc5.Areas.Datadog.Controllers
{
    public class DogHouseController : Controller
    {
        // GET: Datadog/DogHouse
        public ActionResult Index()
        {
            return View();
        }

        // GET: Datadog/Doghouse/Woof
        public string Woof()
        {
            return "WOOF";
        }
    }
}
