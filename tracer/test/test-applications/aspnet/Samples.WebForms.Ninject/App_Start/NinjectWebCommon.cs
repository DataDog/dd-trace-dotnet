[assembly: WebActivatorEx.PreApplicationStartMethod(typeof(Samples.WebForms.Ninject.App_Start.NinjectWebCommon), "Start")]
[assembly: WebActivatorEx.ApplicationShutdownMethodAttribute(typeof(Samples.WebForms.Ninject.App_Start.NinjectWebCommon), "Stop")]

namespace Samples.WebForms.Ninject.App_Start
{
    using System;
    using System.Web;

    using Microsoft.Web.Infrastructure.DynamicModuleHelper;

    using global::Ninject;
    using global::Ninject.Web.Common;
    using global::Ninject.Web.Common.WebHost;

    public static class NinjectWebCommon
    {
        private static readonly Bootstrapper bootstrapper = new Bootstrapper();

        /// <summary>
        /// Starts the application via WebActivator.PreApplicationStartMethod.
        /// This runs BEFORE Application_Start() and before the tracer's
        /// BuildManager.InvokePreStartInitMethodsCore hook.
        /// Ninject's Bootstrapper.Initialize triggers assembly scanning which
        /// creates the temporary "NinjectModuleLoader" AppDomain.
        /// </summary>
        public static void Start()
        {
            DynamicModuleUtility.RegisterModule(typeof(OnePerRequestHttpModule));
            DynamicModuleUtility.RegisterModule(typeof(NinjectHttpModule));
            bootstrapper.Initialize(CreateKernel);
        }

        public static void Stop()
        {
            bootstrapper.ShutDown();
        }

        private static IKernel CreateKernel()
        {
            var kernel = new StandardKernel();
            try
            {
                kernel.Bind<Func<IKernel>>().ToMethod(ctx => () => new Bootstrapper().Kernel);
                kernel.Bind<IHttpModule>().To<HttpApplicationInitializationHttpModule>();
                RegisterServices(kernel);
                return kernel;
            }
            catch
            {
                kernel.Dispose();
                throw;
            }
        }

        private static void RegisterServices(IKernel kernel)
        {
            kernel.Load(System.Reflection.Assembly.GetExecutingAssembly());
        }
    }
}
