using System;
using System.Web.Http;
using Samples.AspNetMvc5.Models;

namespace Samples.Security.WebApi.Controllers
{
    public class HomeController : ApiController
    {
        public void Post([FromBody] MiscModel miscModel)
        {
        }
    }
}
