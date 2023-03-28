using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using System.Web.Mvc;

namespace Samples.Security.WebApi.Controllers
{
    public class HealthController : ApiController
    {
        // GET api/health
        public IEnumerable<string> Get()
        {
            return new [] { "value1", "value2" };
        }

        [ValidateInput(false)]
        public string Get(string id)
        {
            return $"Hello {id}\n";
        }
    }
}
