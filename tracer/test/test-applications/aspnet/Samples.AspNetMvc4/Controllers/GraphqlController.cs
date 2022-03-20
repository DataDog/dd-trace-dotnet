using System.Web.Mvc;

namespace Samples.AspNetMvc4.Controllers
{
    public class GraphqlController: Controller
    {
        public ActionResult Query(string slug)
        {
            return Content(slug);
        }
    }
}
