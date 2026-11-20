using System.Web.Mvc;

namespace Samples.AspNetMvc5.Controllers
{
    // Uses the synchronous ControllerActionInvoker, for which no aspnet-mvc.request span is created, so the
    // child action rendered by its view is the first MVC action of the request. This is the same shape as a
    // WebForms or CMS page (served outside MVC routing) rendering a child action.
    public class SyncParentController : Controller
    {
        protected override bool DisableAsyncSupport
        {
            get { return true; }
        }

        public ActionResult Index()
        {
            return View();
        }
    }
}
