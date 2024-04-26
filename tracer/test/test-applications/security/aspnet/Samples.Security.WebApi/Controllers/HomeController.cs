using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Results;
using Samples.AspNetMvc5.Models;
using Samples.Security.AspNetMvc5.Models;

namespace Samples.Security.WebApi.Controllers
{
    public class HomeController : ApiController
    {
        public void Post([FromBody] MiscModel miscModel)
        {
        }

        [AcceptVerbs("GET")]
        public string Shutdown()
        {
            return "Ok";
        }

        [AcceptVerbs("POST")]
        [Route("api/home/api-security/{id:int}")]
        public IHttpActionResult ApiSecurity(int id, [FromBody] ApiSecurityModel model) => Json(new { Id = model.Dog, Message = $"{model.Dog2}-response" });

        [AcceptVerbs("POST")]
        [Route("api/home/empty-model")]
        public IHttpActionResult EmptyModel([FromBody] ApiSecurityModel model)
        {
            return Json<ApiSecurityModel>(null);
        }
        
        [AcceptVerbs("GET")]
        [Route("api/home/null-action/{pathparam}/{pathparam2}")]
        public object NullAction(string pathparam, string pathparam2)
        {
            return null;
        }
        
        [AcceptVerbs("GET")]
        [Route("api/home/null-action-async/{pathparam}/{pathparam2}")]
        public Task<object> NullActionAsync(string pathparam, string pathparam2)
        {
            return Task.FromResult<object>(null);
        }
    }
}
