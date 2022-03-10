using System.Web.Mvc;

namespace Samples.AspNetMvc5.Controllers
{
    public class GraphqlController: Controller
    {
        public ActionResult Query(string slug)
        {
            return Content(slug);
        }
    }
}
