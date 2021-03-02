using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace Samples.AspNetMvc5.Controllers
{
    public class ConventionsController : System.Web.Http.ApiController
    {
        [HttpGet]
        public IHttpActionResult Delay(int value)
        {
            Thread.Sleep(TimeSpan.FromSeconds(value));
            return Json(value);
        }

        [HttpGet]
        public async Task<IHttpActionResult> DelayAsync(int value)
        {
            await Task.Delay(TimeSpan.FromSeconds(value)).ConfigureAwait(false);
            return Json(value);
        }
        
        [HttpGet]
        public IHttpActionResult Optional(int? value = null)
        {
            Thread.Sleep(TimeSpan.FromSeconds(value ?? 0));
            return Json(value);
        }
        
        [HttpGet]
        public IHttpActionResult StatusCode(int value)
        {
            return this.Content((HttpStatusCode)value, "Status code set to " + value);
        }

        [HttpGet]
        public IHttpActionResult TransientFailure(string value)
        {
            bool success = bool.TryParse(value, out bool result) && result;
            if (success)
            {
                return Json(System.Environment.GetEnvironmentVariables());
            }

            throw new ArgumentException($"Passed in value was not 'true': {value}");
        }
    }
}
