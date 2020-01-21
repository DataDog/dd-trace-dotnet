#if !NETSTANDARD2_0
using System.Web;
using Datadog.Trace.AspNet;

namespace Datadog.Trace.ClrProfiler.Integrations.AspNet
{
    /// <summary>
    /// Contains instrumentation wrappers for basic AspNet such as WebForms
    /// </summary>
    public static class AspNetIntegration
    {
        private const string IntegrationName = "AspNet";
        private const string OperationName = "aspnet.request";

        private const string SystemWebAssemblyName = "System.Web";
        private const string SystemWebHttpApplicationTypeName = "System.Web.HttpApplication";

        /// <summary>
        /// Calls the underlying ExecuteAsync and traces the request.
        /// </summary>
        /// <param name="apiController">The Api Controller</param>
        /// <param name="controllerContext">The controller context for the call</param>
        /// <param name="cancellationTokenSource">The cancellation token source</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>A task with the result</returns>
        [InterceptMethod(
            TargetAssembly = SystemWebAssemblyName,
            TargetType = SystemWebHttpApplicationTypeName,
            TargetSignatureTypes = new[] { ClrNames.Void })]
        public static object InitModules(
            object apiController,
            object controllerContext,
            object cancellationTokenSource,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            // in ASP.NET, we always want to try to use AspNetScopeManager,
            // even if the "AspNet" integration is disabled
            Tracer.Instance = new Tracer(
                settings: null,
                agentWriter: null,
                sampler: null,
                scopeManager: new AspNetScopeManager(),
                statsd: null);

            if (Tracer.Instance.Settings.IsIntegrationEnabled(TracingHttpModule.IntegrationName))
            {
                // only register http module if integration is enabled
                HttpApplication.RegisterModule(typeof(TracingHttpModule));
            }

            return null;
        }
    }
}
#endif
