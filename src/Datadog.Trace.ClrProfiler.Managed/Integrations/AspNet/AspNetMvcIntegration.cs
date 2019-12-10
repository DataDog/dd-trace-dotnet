#if !NETSTANDARD2_0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Routing;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// The ASP.NET MVC integration.
    /// </summary>
    public static class AspNetMvcIntegration
    {
        private const string IntegrationName = "AspNetMvc";
        private const string OperationName = "aspnet-mvc.request";
        private const string HttpContextKey = "__Datadog.Trace.ClrProfiler.Integrations.AspNetMvcIntegration";
        private const string MinimumVersion = "4";
        private const string MaximumVersion = "5";
        private const string AssemblyName = "System.Web.Mvc";

        private const string AsyncActionInvokerTypeName = "System.Web.Mvc.Async.IAsyncActionInvoker";
        private const string ControllerContextTypeName = "System.Web.Mvc.ControllerContext";
        private const string RouteCollectionRouteTypeName = "System.Web.Mvc.Routing.RouteCollectionRoute";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(AspNetMvcIntegration));

        /// <summary>
        /// Creates a scope used to instrument an MVC action and populates some common details.
        /// </summary>
        /// <param name="controllerContext">The System.Web.Mvc.ControllerContext that was passed as an argument to the instrumented method.</param>
        /// <returns>A new scope used to instrument an MVC action.</returns>
        public static Scope CreateScope(object controllerContext)
        {
            Scope scope = null;

            try
            {
                if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
                {
                    // integration disabled, don't create a scope, skip this trace
                    return null;
                }

                if (controllerContext == null || controllerContext.GetType().FullName != ControllerContextTypeName)
                {
                    return null;
                }

                var httpContext = controllerContext.GetProperty<HttpContextBase>("HttpContext").GetValueOrDefault();

                if (httpContext == null)
                {
                    return null;
                }

                string host = httpContext.Request.Headers.Get("Host");
                string httpMethod = httpContext.Request.HttpMethod.ToUpperInvariant();
                string url = httpContext.Request.RawUrl.ToLowerInvariant();
                string resourceName = null;

                RouteData routeData = controllerContext.GetProperty<RouteData>("RouteData").GetValueOrDefault();
                Route route = routeData?.Route as Route;
                RouteValueDictionary routeValues = routeData?.Values;

                if (route == null && routeData?.Route.GetType().FullName == RouteCollectionRouteTypeName)
                {
                    var routeMatches = routeValues?.GetValueOrDefault("MS_DirectRouteMatches") as List<RouteData>;

                    if (routeMatches?.Count > 0)
                    {
                        // route was defined using attribute routing i.e. [Route("/path/{id}")]
                        // get route and routeValues from the RouteData in routeMatches
                        route = routeMatches[0].Route as Route;
                        routeValues = routeMatches[0].Values;

                        if (route != null)
                        {
                            var resourceUrl = route.Url?.ToLowerInvariant() ?? string.Empty;
                            if (resourceUrl.FirstOrDefault() != '/')
                            {
                                resourceUrl = string.Concat("/", resourceUrl);
                            }

                            resourceName = $"{httpMethod} {resourceUrl}";
                        }
                    }
                }

                if (string.IsNullOrEmpty(resourceName) && httpContext.Request.Url != null)
                {
                    var cleanUri = UriHelpers.GetRelativeUrl(httpContext.Request.Url, tryRemoveIds: true);
                    resourceName = $"{httpMethod} {cleanUri.ToLowerInvariant()}";
                }

                string controllerName = (routeValues?.GetValueOrDefault("controller") as string)?.ToLowerInvariant();
                string actionName = (routeValues?.GetValueOrDefault("action") as string)?.ToLowerInvariant();

                if (string.IsNullOrEmpty(resourceName))
                {
                    // Keep the legacy resource name, just to have something
                    resourceName = $"{httpMethod} {controllerName}.{actionName}";
                }

                SpanContext propagatedContext = null;
                var tracer = Tracer.Instance;

                if (tracer.ActiveScope == null)
                {
                    try
                    {
                        // extract propagated http headers
                        var headers = httpContext.Request.Headers.Wrap();
                        propagatedContext = SpanContextPropagator.Instance.Extract(headers);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error extracting propagated HTTP headers.");
                    }
                }

                scope = Tracer.Instance.StartActive(OperationName, propagatedContext);
                Span span = scope.Span;

                // Fail safe to catch templates in routing values
                resourceName =
                    resourceName
                       .Replace("{controller}", controllerName)
                       .Replace("{action}", actionName);

                span.DecorateWebServerSpan(
                    resourceName: resourceName,
                    method: httpMethod,
                    host: host,
                    httpUrl: url);
                span.SetTag(Tags.AspNetRoute, route?.Url);
                span.SetTag(Tags.AspNetController, controllerName);
                span.SetTag(Tags.AspNetAction, actionName);

                // set analytics sample rate if enabled
                var analyticsSampleRate = tracer.Settings.GetIntegrationAnalyticsSampleRate(IntegrationName, enabledWithGlobalSetting: true);
                span.SetMetric(Tags.Analytics, analyticsSampleRate);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        /// <summary>
        /// Wrapper method used to instrument System.Web.Mvc.Async.IAsyncActionInvoker.BeginInvokeAction().
        /// </summary>
        /// <param name="asyncControllerActionInvoker">The IAsyncActionInvoker instance.</param>
        /// <param name="controllerContext">The ControllerContext for the current request.</param>
        /// <param name="actionName">The name of the controller action.</param>
        /// <param name="callback">An <see cref="AsyncCallback"/> delegate.</param>
        /// <param name="state">An object that holds the state of the async operation.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>Returns the <see cref="IAsyncResult "/> returned by the original BeginInvokeAction() that is later passed to <see cref="EndInvokeAction"/>.</returns>
        [InterceptMethod(
            CallerAssembly = AssemblyName,
            TargetAssembly = AssemblyName,
            TargetType = AsyncActionInvokerTypeName,
            TargetSignatureTypes = new[] { ClrNames.IAsyncResult, ControllerContextTypeName, ClrNames.String, ClrNames.AsyncCallback, ClrNames.Object },
            TargetMinimumVersion = MinimumVersion,
            TargetMaximumVersion = MaximumVersion)]
        public static object BeginInvokeAction(
            object asyncControllerActionInvoker,
            object controllerContext,
            object actionName,
            object callback,
            object state,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (asyncControllerActionInvoker == null)
            {
                throw new ArgumentNullException(nameof(asyncControllerActionInvoker));
            }

            Scope scope = null;

            try
            {
                if (HttpContext.Current != null)
                {
                    scope = CreateScope(controllerContext);
                    HttpContext.Current.Items[HttpContextKey] = scope;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error instrumenting method {0}", "System.Web.Mvc.Async.IAsyncActionInvoker.BeginInvokeAction()");
            }

            Func<object, object, object, object, object, object> instrumentedMethod;

            try
            {
                var asyncActionInvokerType = asyncControllerActionInvoker.GetInstrumentedInterface(AsyncActionInvokerTypeName);

                instrumentedMethod = MethodBuilder<Func<object, object, object, object, object, object>>
                                    .Start(moduleVersionPtr, mdToken, opCode, nameof(BeginInvokeAction))
                                    .WithConcreteType(asyncActionInvokerType)
                                    .WithParameters(controllerContext, actionName, callback, state)
                                    .WithNamespaceAndNameFilters(
                                         ClrNames.IAsyncResult,
                                         "System.Web.Mvc.ControllerContext",
                                         ClrNames.String,
                                         ClrNames.AsyncCallback,
                                         ClrNames.Object)
                                    .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: AsyncActionInvokerTypeName,
                    methodName: nameof(BeginInvokeAction),
                    instanceType: asyncControllerActionInvoker.GetType().AssemblyQualifiedName);
                throw;
            }

            try
            {
                // call the original method, inspecting (but not catching) any unhandled exceptions
                return instrumentedMethod(asyncControllerActionInvoker, controllerContext, actionName, callback, state);
            }
            catch (Exception ex)
            {
                scope?.Span.SetException(ex);
                throw;
            }
        }

        /// <summary>
        /// Wrapper method used to instrument System.Web.Mvc.Async.IAsyncActionInvoker.EndInvokeAction().
        /// </summary>
        /// <param name="asyncControllerActionInvoker">The IAsyncActionInvoker instance.</param>
        /// <param name="asyncResult">The <see cref="IAsyncResult"/> returned by <see cref="BeginInvokeAction"/>.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>Returns the <see cref="bool"/> returned by the original EndInvokeAction().</returns>
        [InterceptMethod(
            CallerAssembly = AssemblyName,
            TargetAssembly = AssemblyName,
            TargetType = AsyncActionInvokerTypeName,
            TargetSignatureTypes = new[] { ClrNames.Bool, ClrNames.IAsyncResult },
            TargetMinimumVersion = MinimumVersion,
            TargetMaximumVersion = MaximumVersion)]
        public static bool EndInvokeAction(
            object asyncControllerActionInvoker,
            object asyncResult,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (asyncControllerActionInvoker == null)
            {
                throw new ArgumentNullException(nameof(asyncControllerActionInvoker));
            }

            Scope scope = null;
            var httpContext = HttpContext.Current;

            try
            {
                scope = httpContext?.Items[HttpContextKey] as Scope;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error instrumenting method {0}", $"{AsyncActionInvokerTypeName}.EndInvokeAction()");
            }

            Func<object, object, bool> instrumentedMethod;

            try
            {
                var asyncActionInvokerType = asyncControllerActionInvoker.GetInstrumentedInterface(AsyncActionInvokerTypeName);

                instrumentedMethod = MethodBuilder<Func<object, object, bool>>
                                    .Start(moduleVersionPtr, mdToken, opCode, nameof(EndInvokeAction))
                                    .WithConcreteType(asyncActionInvokerType)
                                    .WithParameters(asyncResult)
                                    .WithNamespaceAndNameFilters(ClrNames.Bool, ClrNames.IAsyncResult)
                                    .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: AsyncActionInvokerTypeName,
                    methodName: nameof(EndInvokeAction),
                    instanceType: asyncControllerActionInvoker.GetType().AssemblyQualifiedName);
                throw;
            }

            try
            {
                // call the original method, inspecting (but not catching) any unhandled exceptions
                return instrumentedMethod(asyncControllerActionInvoker, asyncResult);
            }
            catch (Exception ex)
            {
                scope?.Span.SetException(ex);
                throw;
            }
            finally
            {
                scope?.Dispose();
            }
        }
    }
}

#endif
