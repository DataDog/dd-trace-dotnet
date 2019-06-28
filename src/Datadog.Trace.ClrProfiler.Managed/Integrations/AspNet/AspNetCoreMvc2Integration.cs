using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// The ASP.NET Core MVC 2 integration.
    /// </summary>
    public sealed class AspNetCoreMvc2Integration : IDisposable
    {
        internal const string HttpContextKey = "__Datadog.Trace.ClrProfiler.Integrations." + nameof(AspNetCoreMvc2Integration);
        private const string IntegrationName = "AspNetCoreMvc2";
        private const string OperationName = "aspnet-coremvc.request";
        private const string AspnetMvcCore = "Microsoft.AspNetCore.Mvc.Core";
        private const string Major2 = "2";

        /// <summary>
        /// Type for unobtrusive hooking into Microsoft.AspNetCore.Mvc.Core pipeline.
        /// </summary>
        private const string DiagnosticSource = "Microsoft.AspNetCore.Mvc.Internal.MvcCoreDiagnosticSourceExtensions";

        private static readonly ILog Log = LogProvider.GetLogger(typeof(AspNetCoreMvc2Integration));

        private readonly object _httpContext;
        private readonly Scope _scope;

        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetCoreMvc2Integration"/> class.
        /// </summary>
        /// <param name="actionDescriptor">An ActionDescriptor with information about the current action.</param>
        /// <param name="httpContext">The HttpContext for the current request.</param>
        public AspNetCoreMvc2Integration(object actionDescriptor, object httpContext)
        {
            try
            {
                _httpContext = httpContext;
                var request = _httpContext.GetProperty("Request").GetValueOrDefault();

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

                _scope = tracer.StartActive(OperationName, propagatedContext);
                var span = _scope.Span;

                span.DecorateWebServerSpan(
                    resourceName: resourceName,
                    method: httpMethod,
                    host: host,
                    httpUrl: url);

                span.SetTag(Tags.AspNetController, controllerName);
                span.SetTag(Tags.AspNetAction, actionName);

                if (_httpContext != null &&
                    _httpContext.TryGetPropertyValue("Response", out object response) &&
                    response.TryGetPropertyValue("StatusCode", out object statusCode))
                {
                    span.SetTag(Tags.HttpStatusCode, statusCode.ToString());
                }

                // set analytics sample rate if enabled
                var analyticsSampleRate = tracer.Settings.GetIntegrationAnalyticsSampleRate(IntegrationName, enabledWithGlobalSetting: true);
                span.SetMetric(Tags.Analytics, analyticsSampleRate);
            }
            catch (Exception) when (DisposeObject(_scope))
            {
                // unreachable code
                throw;
            }
        }

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
            AspNetCoreMvc2Integration integration = null;

            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                // integration disabled
                return;
            }

            try
            {
                integration = new AspNetCoreMvc2Integration(actionDescriptor, httpContext);

                if (httpContext.TryGetPropertyValue("Items", out IDictionary<object, object> contextItems))
                {
                    contextItems[HttpContextKey] = integration;
                }
            }
            catch (Exception ex)
            {
                Log.ErrorExceptionForFilter($"Error creating {nameof(AspNetCoreMvc2Integration)}.", ex);
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
            catch (Exception ex) when (integration?.SetException(ex) ?? false)
            {
                // unreachable code
                throw;
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
            var integration = RetrieveFromHttpContext(httpContext);

            if (integration == null)
            {
                    Log.Error($"Could not access {nameof(AspNetCoreMvc2Integration)} for {methodDef}.");
                Log.Error($"Could not access {nameof(AspNetCoreMvc2Integration)}.");
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
                integration?.SetException(ex);

                throw;
            }
            finally
            {
                integration?.Dispose();
            }
        }

        /// <summary>
        /// Entry method for invoking the incoming request pipeline for Microsoft.AspNetCore.Mvc.Core
        /// </summary>
        /// <param name="instance">The instance (this).</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <returns>Task</returns>
        [InterceptMethod(
            TargetAssembly = "Microsoft.AspNetCore.Mvc.Core",
            TargetType = "Microsoft.AspNetCore.Mvc.Internal.ControllerActionInvoker",
            TargetSignatureTypes = new[] { ClrNames.Ignore },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major2)]
        public static object InvokeActionMethodAsync(object instance, int opCode, int mdToken)
        {
            const string methodDef = "Microsoft.AspNetCore.Mvc.Internal.ControllerActionFilter.OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)";
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

            if (shouldTrace)
            {
                AspNetCoreMvc2Integration integration = null;
                if (instance.TryGetFieldValue("_instance", out object controller)
                    && controller.TryGetPropertyValue("HttpContext", out object httpContext))
                {
                    integration = RetrieveFromHttpContext(httpContext);
                }

                if (integration == null)
                {
                    Log.Error($"Could not access {nameof(AspNetCoreMvc2Integration)} for {methodDef}.");
                }

                try
                {
                    // call the original method, catching and rethrowing any unhandled exceptions
                    instrumentedMethod.Invoke(instance, Interception.NoArgObjects);
                }
                catch (Exception ex) when (integration?.SetException(ex) ?? false)
                {
                    // unreachable code
                    throw;
                }
            }

            return instrumentedMethod.Invoke(instance, Interception.NoArgObjects);
        }

        /// <summary>
        /// Tags the current span as an error. Called when an unhandled exception is thrown in the instrumented method.
        /// </summary>
        /// <param name="ex">The exception that was thrown and not handled in the instrumented method.</param>
        /// <returns>Always <c>false</c>.</returns>
        public bool SetException(Exception ex)
        {
            _scope?.Span?.SetException(ex);
            return false;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _scope?.Dispose();
        }

        internal static AspNetCoreMvc2Integration RetrieveFromHttpContext(object httpContext)
        {
            AspNetCoreMvc2Integration integration = null;

            try
            {
                if (httpContext.TryGetPropertyValue("Items", out IDictionary<object, object> contextItems))
                {
                    if (contextItems?.ContainsKey(HttpContextKey) ?? false)
                    {
                        integration = contextItems[HttpContextKey] as AspNetCoreMvc2Integration;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ErrorExceptionForFilter($"Error accessing {nameof(AspNetCoreMvc2Integration)}.", ex);
            }

            return integration;
        }

        internal void SetStatusCode(int statusCode)
        {
            var currentLevel = _scope;
            while (currentLevel != null)
            {
                if (currentLevel.Span != null
                    && !currentLevel.Span.IsFinished
                    && currentLevel.Span.Tags.ContainsKey(Tags.HttpStatusCode))
                {
                    currentLevel.Span.SetTag(Tags.HttpStatusCode, statusCode.ToString());
                }

                currentLevel = currentLevel.Parent;
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

        private bool DisposeObject(IDisposable disposable)
        {
            disposable?.Dispose();
            return false;
        }
    }
}
