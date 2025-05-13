using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Reflection;
using System.Web.Mvc;
using Datadog.Trace;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Samples.AspNet.VersionConflict.Controllers
{
    public class HomeController : Controller
    {
        private static readonly Version _manualTracingVersion = new Version("2.41.0.0");

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

        public ActionResult ParentScope()
        {
            var scope = Tracer.Instance.ActiveScope;

            if (scope == null)
            {
                throw new Exception("Tracer.Instance.ActiveScope is null");
            }

            scope.Span.SetTag("Test", "OK");

            var tagValue = scope.Span.GetTag("Test");

            if (tagValue != "OK")
            {
                throw new Exception("Roundtrip tag test failed: " + (tagValue ?? "{null}"));
            }

            scope.Span.SetTag(Tags.SamplingPriority, "UserKeep");

            return Content(JsonConvert.SerializeObject(scope.Span, typeof(ISpan), new JsonSerializerSettings()));
        }

        public ActionResult SendRequest()
        {
            int result;

            using (Tracer.Instance.StartActive("Manual"))
            {
                using (var innerScope = Tracer.Instance.StartActive("Manual-Inner"))
                {
                    // Create two nested automatic spans to make sure the parent-child relationship is maintained
                    using (StartAutomaticTrace("Automatic-Outer"))
                    {
                        using (var client = new HttpClient())
                        {
                            var target = Url.Action("Index", "Home", null, "http");
                            var content = client.GetStringAsync(target).Result;
                            result = content.Length;
                        }
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

        private static IDisposable StartAutomaticTrace(string operationName)
        {
            // Get the Datadog.Trace.Tracer type from the automatic instrumentation assembly
            Assembly automaticAssembly = AppDomain.CurrentDomain.GetAssemblies().Single(asm => asm.GetName().Name.Equals("Datadog.Trace") && asm.GetName().Version > _manualTracingVersion);
            Type tracerType = automaticAssembly.GetType("Datadog.Trace.Tracer");

            // Invoke 'Tracer.Instance'
            var instanceGetMethod = tracerType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public).GetGetMethod();
            object instance = instanceGetMethod.Invoke(null, new object[] {});

            // Invoke 'public Scope StartActive(string operationName, ISpanContext parent = null, string serviceName = null, DateTimeOffset? startTime = null, bool ignoreActiveScope = false, bool finishOnClose = true)'
            var startActive = tracerType.GetMethod("StartActive", new Type[] { typeof(string) });
            return (IDisposable)startActive.Invoke(instance, new[] { operationName });
        }
    }
}
