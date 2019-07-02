using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// The ASP.NET Core MVC 2 integration.
    /// </summary>
    public static class AspNetCoreMvc2Integration
    {
        private const string IntegrationName = "AspNetCoreMvc2";
        private const string OperationName = "aspnet-coremvc.request";
        private const string AspnetMvcCore = "Microsoft.AspNetCore.Mvc.Core";
        private const string Major2 = "2";

        /// <summary>
        /// Type for unobtrusive hooking into Microsoft.AspNetCore.Mvc.Core pipeline.
        /// </summary>
        private const string DiagnosticSource = "Microsoft.AspNetCore.Mvc.Internal.MvcCoreDiagnosticSourceExtensions";

        /// <summary>
        /// Base type used for traversing the pipeline in Microsoft.AspNetCore.Mvc.Core.
        /// </summary>
        private const string ResourceInvoker = "Microsoft.AspNetCore.Mvc.Internal.ResourceInvoker";

        private static readonly ILog Log = LogProvider.GetLogger(typeof(AspNetCoreMvc2Integration));

        /// <summary>
        /// Wrapper method used to instrument Microsoft.AspNetCore.Mvc.Internal.MvcCoreDiagnosticSourceExtensions.BeforeAction()
        /// </summary>
        /// <param name="diagnosticSource">The DiagnosticSource that this extension method was called on.</param>
        /// <param name="actionDescriptor">An ActionDescriptor with information about the current action.</param>
        /// <param name="httpContext">The HttpContext for the current request.</param>
        /// <param name="routeData">A RouteData with information about the current route.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        [InterceptMethod(
            CallerAssembly = AspnetMvcCore,
            TargetAssembly = AspnetMvcCore,
            TargetType = DiagnosticSource,
            TargetSignatureTypes = new[] { ClrNames.Void, ClrNames.Ignore, "Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor", "Microsoft.AspNetCore.Http.HttpContext", "Microsoft.AspNetCore.Routing.RouteData" },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major2)]
        public static void BeforeAction(
            object diagnosticSource,
            object actionDescriptor,
            object httpContext,
            object routeData,
            int opCode,
            int mdToken)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                // integration disabled
                return;
            }

            string methodDef = $"{DiagnosticSource}.{nameof(BeforeAction)}(...)";
            var integrationContext = AspNetCoreIntegrationContext.RetrieveFromHttpContext(httpContext);

            if (integrationContext == null)
            {
                Log.Error($"Could not access {nameof(AspNetCoreIntegrationContext)} for {methodDef}.");
            }
            else
            {
                SetAspNetCoreMvcSpecificData(
                    integrationContext: integrationContext,
                    actionDescriptor: actionDescriptor,
                    httpContext: httpContext);
            }

            MethodBase instrumentedMethod = null;

            try
            {
                instrumentedMethod = Assembly.GetCallingAssembly().ManifestModule.ResolveMethod(mdToken);
            }
            catch (Exception ex)
            {
                // profiled app will continue working as expected without this method
                Log.ErrorException($"Error calling {DiagnosticSource}.{nameof(BeforeAction)}(...)", ex);
            }

            try
            {
                // call the original method, catching and rethrowing any unhandled exceptions
                instrumentedMethod?.Invoke(null, new[] { diagnosticSource, actionDescriptor, httpContext, routeData });
            }
            catch (Exception ex)
            {
                // profiled app will continue working as expected without this method
                Log.Error($"Exception when calling {methodDef}.", ex);
            }
        }

        /// <summary>
        /// Wrapper method used to instrument Microsoft.AspNetCore.Mvc.Internal.MvcCoreDiagnosticSourceExtensions.AfterAction()
        /// </summary>
        /// <param name="diagnosticSource">The DiagnosticSource that this extension method was called on.</param>
        /// <param name="actionDescriptor">An ActionDescriptor with information about the current action.</param>
        /// <param name="httpContext">The HttpContext for the current request.</param>
        /// <param name="routeData">A RouteData with information about the current route.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        [InterceptMethod(
            CallerAssembly = AspnetMvcCore,
            TargetAssembly = AspnetMvcCore,
            TargetType = DiagnosticSource,
            TargetSignatureTypes = new[] { ClrNames.Void, ClrNames.Ignore, "Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor", "Microsoft.AspNetCore.Http.HttpContext", "Microsoft.AspNetCore.Routing.RouteData" },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major2)]
        public static void AfterAction(
            object diagnosticSource,
            object actionDescriptor,
            object httpContext,
            object routeData,
            int opCode,
            int mdToken)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                // integration disabled
                return;
            }

            string methodDef = $"{DiagnosticSource}.{nameof(AfterAction)}(...)";
            var integrationContext = AspNetCoreIntegrationContext.RetrieveFromHttpContext(httpContext);

            Scope aspNetCoreMvcActionScope = null;

            if (integrationContext == null)
            {
                Log.Error($"Could not access {nameof(AspNetCoreIntegrationContext)} for {methodDef}.");
            }
            else
            {
                integrationContext.TryRetrieveScope(IntegrationName, out aspNetCoreMvcActionScope);
            }

            MethodBase instrumentedMethod = null;

            try
            {
                instrumentedMethod = Assembly.GetCallingAssembly().ManifestModule.ResolveMethod(mdToken);
            }
            catch (Exception ex)
            {
                // profiled app will continue working as expected without this method
                Log.ErrorException($"Error retrieving {methodDef}", ex);
            }

            try
            {
                // call the original method, catching and rethrowing any unhandled exceptions
                instrumentedMethod?.Invoke(null, new[] { diagnosticSource, actionDescriptor, httpContext, routeData });
            }
            catch (Exception ex)
            {
                // profiled app will continue working as expected without this method
                Log.Error($"Exception when calling {methodDef}.", ex);
            }
            finally
            {
                aspNetCoreMvcActionScope?.Dispose();
            }
        }

        /// <summary>
        /// Wrapper method used to catch unhandled exceptions in the incoming request pipeline for Microsoft.AspNetCore.Mvc.Core
        /// </summary>
        /// <param name="context">The DiagnosticSource that this extension method was called on.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        [InterceptMethod(
            CallerAssembly = AspnetMvcCore,
            TargetAssembly = AspnetMvcCore,
            TargetType = ResourceInvoker,
            TargetSignatureTypes = new[] { ClrNames.Void, ClrNames.Ignore },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major2)]
        public static void Rethrow(object context, int opCode, int mdToken)
        {
            string methodDef = $"{ResourceInvoker}.{nameof(Rethrow)}({context?.GetType().FullName} context)";
            var shouldTrace = Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName);
            MethodBase instrumentedMethod;

            try
            {
                instrumentedMethod = Assembly.GetCallingAssembly().ManifestModule.ResolveMethod(mdToken);
            }
            catch (Exception ex)
            {
                // profiled app will not continue working as expected without this method
                Log.ErrorException($"Error retrieving {methodDef}", ex);
                throw;
            }

            AspNetCoreIntegrationContext integration = null;

            if (shouldTrace)
            {
                var httpContextResult = context.GetProperty("HttpContext");

                if (httpContextResult.HasValue)
                {
                    integration = AspNetCoreIntegrationContext.RetrieveFromHttpContext(httpContextResult.Value);
                }

                if (integration == null)
                {
                    Log.Error($"Could not access {nameof(AspNetCoreIntegrationContext)} for {methodDef}.");
                }
            }

            try
            {
                // call the original method, catching and rethrowing any unhandled exceptions
                instrumentedMethod.Invoke(null, new[] { context });
            }
            catch (Exception ex) when (integration?.SetExceptionOnRootSpan(ex) ?? false)
            {
                // unreachable code
                throw;
            }
        }

        private static void SetAspNetCoreMvcSpecificData(
            AspNetCoreIntegrationContext integrationContext,
            object actionDescriptor,
            object httpContext)
        {
            try
            {
                var request = httpContext.GetProperty("Request").GetValueOrDefault();

                GetTagValues(
                    actionDescriptor: actionDescriptor,
                    request: request,
                    httpMethod: out string httpMethod,
                    resourceName: out string resourceName,
                    controllerName: out string controllerName,
                    actionName: out string actionName);

                integrationContext.ResetWebServerRootTags(
                    operationName: OperationName,
                    resourceName: resourceName,
                    method: httpMethod);

                var aspNetCoreMvcActionScope = integrationContext.Tracer.StartActive("aspnet-coremvc.action");

                aspNetCoreMvcActionScope.Span?.SetTag(Tags.AspNetController, controllerName);
                aspNetCoreMvcActionScope.Span?.SetTag(Tags.AspNetAction, actionName);

                integrationContext.TryPersistScope(IntegrationName, aspNetCoreMvcActionScope);

                // set analytic sample rate if enabled
                var analyticSampleRate = integrationContext.Tracer.Settings.GetIntegrationAnalyticsSampleRate(IntegrationName, enabledWithGlobalSetting: true);
                integrationContext.SetMetricOnRootSpan(Tags.Analytics, analyticSampleRate);
            }
            catch (Exception ex)
            {
                // swallow, don't crash applications
                Log.Error($"Exception when decorating tags per {nameof(AspNetCoreMvc2Integration)}", ex);
            }
        }

        private static void GetTagValues(
            object actionDescriptor,
            object request,
            out string httpMethod,
            out string resourceName,
            out string controllerName,
            out string actionName)
        {
            controllerName = actionDescriptor.GetProperty<string>("ControllerName").GetValueOrDefault()?.ToLowerInvariant();

            actionName = actionDescriptor.GetProperty<string>("ActionName").GetValueOrDefault()?.ToLowerInvariant();

            string host = request.GetProperty("Host").GetProperty<string>("Value").GetValueOrDefault();

            httpMethod = request.GetProperty<string>("Method").GetValueOrDefault()?.ToUpperInvariant() ?? "UNKNOWN";

            string pathBase = request.GetProperty("PathBase").GetProperty<string>("Value").GetValueOrDefault();

            string path = request.GetProperty("Path").GetProperty<string>("Value").GetValueOrDefault();

            string queryString = request.GetProperty("QueryString").GetProperty<string>("Value").GetValueOrDefault();

            string url = $"{pathBase}{path}{queryString}";

            string resourceUrl = actionDescriptor.GetProperty("AttributeRouteInfo").GetProperty<string>("Template").GetValueOrDefault() ??
                                 UriHelpers.GetRelativeUrl(new Uri($"https://{host}{url}"), tryRemoveIds: true).ToLowerInvariant();

            resourceName = $"{httpMethod} {resourceUrl}";
        }
    }
}
