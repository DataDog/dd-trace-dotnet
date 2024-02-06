using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using System.Web.Mvc;

namespace Samples.Security.WebApi.Controllers
{
    public class LandingController : ApiController
    {
        [System.Web.Http.AcceptVerbs("GET")]
        public IHttpActionResult Index()
        {
            return Ok();
        }
    }
}
