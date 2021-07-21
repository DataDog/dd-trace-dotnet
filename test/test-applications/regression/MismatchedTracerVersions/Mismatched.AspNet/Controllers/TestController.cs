using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace MismatchedTracerVersions.AspNet.Controllers
{
    public class TestController : ApiController
    {
        [Route("assemblies")]
        public IEnumerable<string> GetAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                                          .Select(a => a.FullName)
                                          .Where(a => a.StartsWith("Datadog"))
                                          .Distinct()
                                          .OrderBy(a => a);
        }

        [Route("timestamp")]
        public string GetTimestamp()
        {
            return DateTime.UtcNow.ToString();
        }
    }
}
