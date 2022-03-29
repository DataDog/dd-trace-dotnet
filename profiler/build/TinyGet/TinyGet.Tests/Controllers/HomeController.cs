using System;
using System.Threading.Tasks;
using System.Web.Http;

namespace TinyGet.Tests.Controllers
{
    public class HomeController : ApiController
    {
        public String Get()
        {
            RequestRecorder.Increment("HomeController.Get");
            return "Home controller";
        }

        [Route("home/long-running")]
        public async Task<String> GetLongRunning()
        {
            RequestRecorder.Increment("HomeController.GetLongRunning");
            await Task.Delay(TimeSpan.FromMinutes(10));
            return "Home controller";
        }
    }
}
