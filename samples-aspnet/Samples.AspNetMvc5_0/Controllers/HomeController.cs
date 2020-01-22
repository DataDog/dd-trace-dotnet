using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Datadog.Trace;

namespace Samples.AspNetMvc5_0.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            var prefixes = new[] { "COR_", "CORECLR_", "DD_", "DATADOG_" };

            var envVars = from envVar in Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
                          from prefix in prefixes
                          let key = (envVar.Key as string)?.ToUpperInvariant()
                          let value = envVar.Value as string
                          where key.StartsWith(prefix)
                          orderby key
                          select new KeyValuePair<string, string>(key, value);

            return View(envVars.ToList());
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        public async Task<ActionResult> DadJoke()
        {
            string responseText;
            var request = WebRequest.Create("https://icanhazdadjoke.com/");
            ((HttpWebRequest)request).Accept = "application/json;q=1";

            using (var response = await request.GetResponseAsync())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                try
                {
                    responseText = reader.ReadToEnd();
                }
                catch (Exception)
                {
                    responseText = "ENCOUNTERED AN ERROR WHEN READING RESPONSE.";
                }
            }

            using (var scope = Tracer.Instance.StartActive("not-http-request-child-first"))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1_000));
            }

            request = WebRequest.Create("https://icanhazdadjoke.com/");
            ((HttpWebRequest)request).Accept = "application/json;q=1";

            using (var response = await request.GetResponseAsync())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                try
                {
                    responseText = reader.ReadToEnd();
                }
                catch (Exception)
                {
                    responseText = "ENCOUNTERED AN ERROR WHEN READING RESPONSE.";
                }
            }

            using (var scope = Tracer.Instance.StartActive("not-http-request-child-second"))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1_000));
            }

            return Json(responseText, JsonRequestBehavior.AllowGet);
        }

        public ActionResult BadRequest()
        {
            throw new Exception("Oops, it broke.");
        }
    }
}
