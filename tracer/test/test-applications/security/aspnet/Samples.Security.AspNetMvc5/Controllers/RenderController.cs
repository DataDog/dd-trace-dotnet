using System.Text;
using System.Web.Mvc;

namespace Samples.Security.AspNetCore5.Controllers
{
    public class RenderController : Controller
    {
        public ActionResult Index(string str)
        {
            var sb = new StringBuilder();

            for (var i = 0; i < 10000; i++)
            {
                sb.Append(str);
            }
            return Content(sb.ToString());
        }
    }
}
