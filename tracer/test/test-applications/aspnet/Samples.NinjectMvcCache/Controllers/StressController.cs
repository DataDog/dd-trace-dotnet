using System;
using System.Diagnostics;
using System.Linq;
using System.Web.Mvc;
using Samples.NinjectMvcCache.Diagnostics;
using Samples.NinjectMvcCache.Models;

namespace Samples.NinjectMvcCache.Controllers
{
    public class StressController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            return Json(
                new
                {
                    Application = "APMS-20314 Ninject MVC activation-cache reproduction",
                    Mode = MvcApplication.ReproductionMode,
                    AddImplicitRequiredAttributeForValueTypes = DataAnnotationsModelValidatorProvider.AddImplicitRequiredAttributeForValueTypes,
                    Endpoints = new[]
                    {
                        "GET /Stress/Run?iterations=250&includeCache=true",
                        "GET /Stress/Cache",
                        "POST /Stress/Collect",
                    },
                },
                JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult Run(int iterations = 250, bool includeCache = false)
        {
            if (iterations < 1 || iterations > 10000)
            {
                return new HttpStatusCodeResult(400, "iterations must be between 1 and 10000");
            }

            var model = new ReproductionModel();
            var metadata = ModelMetadataProviders.Current.GetMetadataForProperties(model, typeof(ReproductionModel)).ToArray();
            var initialGen2Collections = GC.CollectionCount(2);
            var stopwatch = Stopwatch.StartNew();
            var validatorCount = 0;

            for (var iteration = 0; iteration < iterations; iteration++)
            {
                foreach (var propertyMetadata in metadata)
                {
                    validatorCount += ModelValidatorProviders.Providers
                                                             .GetValidators(propertyMetadata, ControllerContext)
                                                             .Count();
                }
            }

            stopwatch.Stop();
            return Json(
                new
                {
                    Mode = MvcApplication.ReproductionMode,
                    Iterations = iterations,
                    PropertiesPerIteration = metadata.Length,
                    ValidatorsCreated = validatorCount,
                    ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                    Gen2Collections = GC.CollectionCount(2) - initialGen2Collections,
                    // Cache inspection takes Ninject's cache lock and enumerates the
                    // backing HashSet. Keep it off the measured load path by default.
                    Cache = includeCache ? ActivationCacheInspector.Capture(MvcApplication.Kernel) : null,
                },
                JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult Cache()
        {
            return Json(ActivationCacheInspector.Capture(MvcApplication.Kernel), JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult Collect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return Json(new { Collected = true, Cache = ActivationCacheInspector.Capture(MvcApplication.Kernel) });
        }
    }
}
