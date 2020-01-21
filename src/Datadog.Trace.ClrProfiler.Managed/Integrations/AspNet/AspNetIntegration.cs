#if !NETSTANDARD2_0
using System;
using System.Web;
using Datadog.Trace.AspNet;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Logging;

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

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(AspNetIntegration));

        /// <summary>
        /// Calls the underlying ExecuteAsync and traces the request.
        /// </summary>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        [InterceptMethod(
            TargetAssembly = SystemWebAssemblyName,
            TargetType = SystemWebHttpApplicationTypeName,
            TargetSignatureTypes = new[] { ClrNames.Void })]
        public static void InitModules(
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            Func<HttpApplication> instrumentedMethod;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<HttpApplication>>
                       .Start(moduleVersionPtr, mdToken, opCode, nameof(InitModules))
                       .WithConcreteType(typeof(HttpApplication))
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: SystemWebHttpApplicationTypeName,
                    methodName: nameof(InitModules),
                    instanceType: SystemWebHttpApplicationTypeName);
                throw;
            }

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
        }
    }
}
#endif
