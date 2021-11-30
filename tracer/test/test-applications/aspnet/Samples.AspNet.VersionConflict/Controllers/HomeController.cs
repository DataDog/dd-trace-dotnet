using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Web.Mvc;
using Datadog.Trace;

namespace Samples.AspNet.VersionConflict.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            var envVars = SampleHelpers.GetDatadogEnvironmentVariables();

            return View(envVars.ToList());
        }

        public ActionResult Shutdown()
        {
            SampleHelpers.RunShutDownTasks(this);

            return RedirectToAction("Index");
        }

        public ActionResult SendRequest()
        {
            int result = 0;

            using (Tracer.Instance.StartActive("Manual"))
            {
                using (var innerScope = Tracer.Instance.StartActive("Manual-Inner"))
                {
                    using (var client = new HttpClient())
                    {
                        var target = Url.Action("Index", "Home", null, "http");
                        var content = client.GetStringAsync(target).Result;
                        result = content.Length;
                    }
                }
            }

            return View(result);
        }

        public ActionResult Sampling(bool parentTrace = true)
        {
            if (parentTrace)
            {
                CreateTraces();
            }
            else
            {
                // Same test but without a parent automatic trace
                var mutex = new ManualResetEventSlim();

                ThreadPool.UnsafeQueueUserWorkItem(_ =>
                {
                    try
                    {
                        CreateTraces();
                    }
                    finally
                    {
                        mutex.Set();
                    }

                }, null);

                mutex.Wait();
            }
            
            return View();
        }

        private void CreateTraces()
        {
            using (var scope = Tracer.Instance.StartActive("Manual"))
            {
                scope.Span.SetTag(Tags.SamplingPriority, "UserKeep");

                using (var innerScope = Tracer.Instance.StartActive("Manual-Inner"))
                {
                    using (var client = new HttpClient())
                    {
                        var target = Url.Action("Index", "Home", null, "http");

                        _ = client.GetStringAsync(target).Result;

                        // This should be ignored because the sampling priority has been locked
                        scope.Span.SetTag(Tags.SamplingPriority, "UserReject");

                        _ = client.GetStringAsync(target).Result;

                        Tracer.Instance.StartActive("Child").Dispose();
                    }
                }
            }
        }
    }
}
