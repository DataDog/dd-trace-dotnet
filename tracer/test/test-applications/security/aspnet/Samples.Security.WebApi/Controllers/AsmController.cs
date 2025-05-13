using System.Web.Http;
using Samples.AspNetMvc5.Models;

namespace Samples.Security.WebApi.Controllers
{
    public class AsmController : ApiController
    {
        [AcceptVerbs("GET")]
        [Route("api/asm/injectedheader")]
        public string InjectedHeader()
        {
            System.Web.HttpContext.Current.Response.AddHeader("X-Test", "dummy_rule");
            return "Header injected!";
        }
    }
}
