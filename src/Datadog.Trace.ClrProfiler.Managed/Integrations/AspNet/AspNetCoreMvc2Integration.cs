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
    public sealed class AspNetCoreMvc2Integration
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
            var integration = WebServerIntegrationContext.RetrieveFromHttpContext(httpContext);

            if (integration == null)
            {
                Log.Error($"Could not access {nameof(WebServerIntegrationContext)} for {methodDef}.");
            }
            else
            {
                SetAspNetCoreMvcSpecificData(
                    integrationContext: integration,
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
            var integration = WebServerIntegrationContext.RetrieveFromHttpContext(httpContext);

            if (integration == null)
            {
                Log.Error($"Could not access {nameof(WebServerIntegrationContext)} for {methodDef}.");
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

            WebServerIntegrationContext integration = null;

            if (shouldTrace)
            {
                var httpContextResult = context.GetProperty("HttpContext");

                if (httpContextResult.HasValue)
                {
                    integration = WebServerIntegrationContext.RetrieveFromHttpContext(httpContextResult.Value);
                }

                if (integration == null)
                {
                    Log.Error($"Could not access {nameof(WebServerIntegrationContext)} for {methodDef}.");
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
            WebServerIntegrationContext integrationContext,
            object actionDescriptor,
            object httpContext)
        {
            try
            {
                var request = httpContext.GetProperty("Request").GetValueOrDefault();

                GetTagValues(
                    actionDescriptor,
                    request,
                    out string httpMethod,
                    out string host,
                    out string resourceName,
                    out string url,
                    out string controllerName,
                    out string actionName);

                SpanContext propagatedContext = null;
                var tracer = Tracer.Instance;

                if (tracer.ActiveScope == null)
                {
                    try
                    {
                        // extract propagated http headers
                        var requestHeaders = request.GetProperty<IEnumerable>("Headers").GetValueOrDefault();

                        if (requestHeaders != null)
                        {
                            var headersCollection = new DictionaryHeadersCollection();

                            foreach (object header in requestHeaders)
                            {
                                var key = header.GetProperty<string>("Key").GetValueOrDefault();
                                var values = header.GetProperty<IList<string>>("Value").GetValueOrDefault();

                                if (key != null && values != null)
                                {
                                    headersCollection.Add(key, values);
                                }
                            }

                            propagatedContext = SpanContextPropagator.Instance.Extract(headersCollection);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.ErrorException("Error extracting propagated HTTP headers.", ex);
                    }
                }

                integrationContext.ResetWebServerRootTags(
                    operationName: OperationName,
                    resourceName: resourceName,
                    method: httpMethod,
                    host: host,
                    httpUrl: url);

                integrationContext.SetTagOnRootSpan(Tags.AspNetController, controllerName);
                integrationContext.SetTagOnRootSpan(Tags.AspNetAction, actionName);

                // set analytics sample rate if enabled
                var analyticsSampleRate = tracer.Settings.GetIntegrationAnalyticsSampleRate(IntegrationName, enabledWithGlobalSetting: true);
                integrationContext.SetMetricOnRootSpan(Tags.Analytics, analyticsSampleRate);
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
            out string host,
            out string resourceName,
            out string url,
            out string controllerName,
            out string actionName)
        {
            controllerName = actionDescriptor.GetProperty<string>("ControllerName").GetValueOrDefault()?.ToLowerInvariant();

            actionName = actionDescriptor.GetProperty<string>("ActionName").GetValueOrDefault()?.ToLowerInvariant();

            host = request.GetProperty("Host").GetProperty<string>("Value").GetValueOrDefault();

            httpMethod = request.GetProperty<string>("Method").GetValueOrDefault()?.ToUpperInvariant() ?? "UNKNOWN";

            string pathBase = request.GetProperty("PathBase").GetProperty<string>("Value").GetValueOrDefault();

            string path = request.GetProperty("Path").GetProperty<string>("Value").GetValueOrDefault();

            string queryString = request.GetProperty("QueryString").GetProperty<string>("Value").GetValueOrDefault();

            url = $"{pathBase}{path}{queryString}";

            string resourceUrl = actionDescriptor.GetProperty("AttributeRouteInfo").GetProperty<string>("Template").GetValueOrDefault() ??
                                 UriHelpers.GetRelativeUrl(new Uri($"https://{host}{url}"), tryRemoveIds: true).ToLowerInvariant();

            resourceName = $"{httpMethod} {resourceUrl}";
        }
    }
}
