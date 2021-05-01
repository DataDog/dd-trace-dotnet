using System.Web.Http;

namespace Samples.Owin.WebApi2.Controllers
{
    [RoutePrefix("")]
    public class HomeController : System.Web.Http.ApiController
    {
        [HttpGet]
        [Route("alive-check")]
        public string IsAlive()
        {
            return "Yes";
        }
    }
}
