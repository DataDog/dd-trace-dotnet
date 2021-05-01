using System.Web.Http;

namespace Samples.Owin.WebApi.Controllers
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
