using System.Web.Mvc;
using Website_AspNet.Models;

namespace Website_AspNet.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index(int? number)
        {
            var data = Fibonacci.Compute(number);
            var model = new IndexModel(data);
            return View(model);
        }
    }
}
