using System;
using System.Web.Http;
using Samples.AspNetMvc5.Models;
using Samples.Security.WebApi.Models;

namespace Samples.Security.WebApi.Controllers
{
    public class RouteController : ApiController
    {
        public string Get(AnEnum id)
        {
            return id.ToString();
        }
    }
}
