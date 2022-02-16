using System.Collections.Generic;
using System.Web.Http;

namespace Samples.Security.WebApi.Controllers
{
    public class HealthController : ApiController
    {
        // GET api/health
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

    }
}
