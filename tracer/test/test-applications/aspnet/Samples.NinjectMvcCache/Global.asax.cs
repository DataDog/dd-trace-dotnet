using System;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Ninject;
using Ninject.Activation.Caching;
using Ninject.Components;
using Ninject.Web.Mvc;
using Ninject.Web.Mvc.Validation;
using Samples.NinjectMvcCache.Infrastructure;

namespace Samples.NinjectMvcCache
{
    public class MvcApplication : HttpApplication
    {
        internal static IKernel Kernel { get; private set; }

        internal static string ReproductionMode { get; private set; }

        protected void Application_Start()
        {
            ReproductionMode = ConfigurationManager.AppSettings["ReproductionMode"] ?? "NinjectValidator";

            var kernel = new StandardKernel();
            if (ReproductionMode.Equals("CacheDisabled", StringComparison.OrdinalIgnoreCase))
            {
                kernel.Components.RemoveAll<IActivationCache>();
                kernel.Components.Add<IActivationCache, NoOpActivationCache>();
            }

            Kernel = kernel;
            DependencyResolver.SetResolver(new NinjectDependencyResolver(kernel));

            DataAnnotationsModelValidatorProvider.AddImplicitRequiredAttributeForValueTypes = true;
            ReplaceDataAnnotationsValidatorProvider(kernel, ReproductionMode);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }

        protected void Application_End()
        {
            Kernel?.Dispose();
        }

        private static void ReplaceDataAnnotationsValidatorProvider(IKernel kernel, string mode)
        {
            var providers = ModelValidatorProviders.Providers;
            foreach (var provider in providers.OfType<DataAnnotationsModelValidatorProvider>().ToArray())
            {
                providers.Remove(provider);
            }

            if (mode.Equals("DefaultValidator", StringComparison.OrdinalIgnoreCase))
            {
                providers.Add(new DataAnnotationsModelValidatorProvider());
                return;
            }

            providers.Add(new NinjectDataAnnotationsModelValidatorProvider(kernel));
        }
    }
}
