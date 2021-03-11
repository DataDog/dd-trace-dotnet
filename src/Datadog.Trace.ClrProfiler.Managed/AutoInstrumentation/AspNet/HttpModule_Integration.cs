#if NETFRAMEWORK
using System.Threading;
using System.Web;
using Datadog.Trace.AspNet;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// System.Web.Compilation.BuildManager.InvokePreStartInitMethodsCore calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Web",
        TypeName = "System.Web.Compilation.BuildManager",
        MethodName = "InvokePreStartInitMethodsCore",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "System.Collections.Generic.ICollection`1[System.Reflection.MethodInfo]", "System.Func`1[System.IDisposable]" },
        MinimumVersion = "4.0.0",
        MaximumVersion = "4.*.*",
        IntegrationName = IntegrationName)]
    public class HttpModule_Integration
    {
        private const string IntegrationName = nameof(IntegrationIds.AspNet);

        /// <summary>
        /// Indicates whether we're initializing the HttpModule for the first time
        /// </summary>
        private static int _firstInitialization = 1;

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TCollection">Type of the collection</typeparam>
        /// <typeparam name="TFunc">Type of the </typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method. This method is static so this parameter will always be null</param>
        /// <param name="methods">The methods to be invoked</param>
        /// <param name="setHostingEnvironmentCultures">The function to set the environment culture</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TCollection, TFunc>(TTarget instance, TCollection methods, TFunc setHostingEnvironmentCultures)
        {
            if (Interlocked.Exchange(ref _firstInitialization, 0) != 1)
            {
                // The HttpModule was already registered
                return CallTargetState.GetDefault();
            }

            try
            {
                HttpApplication.RegisterModule(typeof(TracingHttpModule));
            }
            catch
            {
                // Unable to dynamically register module
                // Not sure if we can technically log yet or not, so do nothing
            }

            return CallTargetState.GetDefault();
        }
    }
}
#endif
