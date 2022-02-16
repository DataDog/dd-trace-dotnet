using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Mvc;

namespace Samples.AspNetMvc4.Controllers
{
    public class ErrorController : Controller
    {
        // GET: Error
        public virtual ActionResult Index()
        {
            Response.Headers.Add("X-Test-Mvc-Response-Header", "true");
            Response.TrySkipIisCustomErrors = true;

            if (Request.QueryString["ErrorStatusCode"] is string statusCodeString
                && int.TryParse(statusCodeString, out int statusCode))
            {
                Response.StatusCode = statusCode;
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }

            var errorInformationDict = new Dictionary<string, string>()
            {
                { "ErrorId", Request.QueryString["ErrorId"] }
            };

            return View(errorInformationDict.ToList());
        }
    }
}
