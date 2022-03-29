using System;
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

        [HttpGet]
        [Route("shutdown")]
        public string Shutdown()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.DefinedTypes)
                {
                    if (type.Namespace == "Coverlet.Core.Instrumentation.Tracker")
                    {
                        var unloadModuleMethod = type.GetMethod("UnloadModule", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        unloadModuleMethod.Invoke(null, new object[] { this, EventArgs.Empty });
                    }
                }
            }

            return "Code coverage data has been flushed";
        }
    }
}
