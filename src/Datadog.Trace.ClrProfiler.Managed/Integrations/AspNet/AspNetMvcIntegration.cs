// <copyright file="AspNetMvcIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Routing;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Transport.Http;
using Datadog.Trace.AspNet;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Integrations.AspNet;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Http;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// The ASP.NET MVC integration.
    /// </summary>
    public static class AspNetMvcIntegration
    {
        internal const string HttpContextKey = "__Datadog.Trace.ClrProfiler.Integrations.AspNetMvcIntegration";
        private const string OperationName = "aspnet-mvc.request";
        private const string MinimumVersion = "4";
        private const string MaximumVersion = "5";
        private const string AssemblyName = "System.Web.Mvc";

        private const string AsyncActionInvokerTypeName = "System.Web.Mvc.Async.IAsyncActionInvoker";
        private const string ControllerContextTypeName = "System.Web.Mvc.ControllerContext";
        private const string RouteCollectionRouteTypeName = "System.Web.Mvc.Routing.RouteCollectionRoute";

        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.AspNetMvc));
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AspNetMvcIntegration));

        /// <summary>
        /// Creates a scope used to instrument an MVC action and populates some common details.
        /// </summary>
        /// <param name="controllerContext">The System.Web.Mvc.ControllerContext that was passed as an argument to the instrumented method.</param>
        /// <returns>A new scope used to instrument an MVC action.</returns>
        public static Scope CreateScope(ControllerContextStruct controllerContext)
        {
            Scope scope = null;

            try
            {
                var httpContext = controllerContext.HttpContext;

                if (httpContext == null)
                {
                    return null;
                }

                Span span = null;
                // integration enabled, go create a scope!
                if (Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId))
                {
                    var newResourceNamesEnabled = Tracer.Instance.Settings.RouteTemplateResourceNamesEnabled;
                    string host = httpContext.Request.Headers.Get("Host");
                    string httpMethod = httpContext.Request.HttpMethod.ToUpperInvariant();
                    string url = httpContext.Request.RawUrl.ToLowerInvariant();
                    string resourceName = null;

                    RouteData routeData = controllerContext.RouteData;
                    Route route = routeData?.Route as Route;
                    RouteValueDictionary routeValues = routeData?.Values;
                    bool wasAttributeRouted = false;

                    if (route == null && routeData?.Route.GetType().FullName == RouteCollectionRouteTypeName)
                    {
                        var routeMatches = routeValues?.GetValueOrDefault("MS_DirectRouteMatches") as List<RouteData>;

                        if (routeMatches?.Count > 0)
                        {
                            // route was defined using attribute routing i.e. [Route("/path/{id}")]
                            // get route and routeValues from the RouteData in routeMatches
                            wasAttributeRouted = true;
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

                    string routeUrl = route?.Url;
                    string areaName = (routeValues?.GetValueOrDefault("area") as string)?.ToLowerInvariant();
                    string controllerName = (routeValues?.GetValueOrDefault("controller") as string)?.ToLowerInvariant();
                    string actionName = (routeValues?.GetValueOrDefault("action") as string)?.ToLowerInvariant();

                    if (newResourceNamesEnabled && string.IsNullOrEmpty(resourceName) && !string.IsNullOrEmpty(routeUrl))
                    {
                        resourceName = $"{httpMethod} /{routeUrl.ToLowerInvariant()}";
                    }

                    if (string.IsNullOrEmpty(resourceName) && httpContext.Request.Url != null)
                    {
                        var cleanUri = UriHelpers.GetCleanUriPath(httpContext.Request.Url);
                        resourceName = $"{httpMethod} {cleanUri.ToLowerInvariant()}";
                    }

                    if (string.IsNullOrEmpty(resourceName))
                    {
                        // Keep the legacy resource name, just to have something
                        resourceName = $"{httpMethod} {controllerName}.{actionName}";
                    }

                    // Replace well-known routing tokens
                    resourceName =
                        resourceName
                           .Replace("{area}", areaName)
                           .Replace("{controller}", controllerName)
                           .Replace("{action}", actionName);

                    if (newResourceNamesEnabled && !wasAttributeRouted && routeValues is not null && route is not null)
                    {
                        // Remove unused parameters from conventional route templates
                        // Don't bother with routes defined using attribute routing
                        foreach (var parameter in route.Defaults)
                        {
                            var parameterName = parameter.Key;
                            if (parameterName != "area"
                                && parameterName != "controller"
                                && parameterName != "action"
                                && !routeValues.ContainsKey(parameterName))
                            {
                                resourceName = resourceName.Replace($"/{{{parameterName}}}", string.Empty);
                            }
                        }
                    }

                    SpanContext propagatedContext = null;
                    var tracer = Tracer.Instance;
                    var tagsFromHeaders = Enumerable.Empty<KeyValuePair<string, string>>();

                    if (tracer.ActiveScope == null)
                    {
                        try
                        {
                            // extract propagated http headers
                            var headers = httpContext.Request.Headers.Wrap();
                            propagatedContext = SpanContextPropagator.Instance.Extract(headers);
                            tagsFromHeaders = SpanContextPropagator.Instance.ExtractHeaderTags(headers, tracer.Settings.HeaderTags, SpanContextPropagator.HttpRequestHeadersTagPrefix);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error extracting propagated HTTP headers.");
                        }
                    }

                    var tags = new AspNetTags();
                    scope = Tracer.Instance.StartActiveWithTags(OperationName, propagatedContext, tags: tags);
                    span = scope.Span;

                    span.DecorateWebServerSpan(
                        resourceName: resourceName,
                        method: httpMethod,
                        host: host,
                        httpUrl: url,
                        tags,
                        tagsFromHeaders);

                    tags.AspNetRoute = routeUrl;
                    tags.AspNetArea = areaName;
                    tags.AspNetController = controllerName;
                    tags.AspNetAction = actionName;

                    tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: true);

                    if (newResourceNamesEnabled)
                    {
                        // set the resource name in the HttpContext so TracingHttpModule can update root span
                        httpContext.Items[SharedConstants.HttpContextPropagatedResourceNameKey] = resourceName;
                    }
                }

                var security = Security.Instance;
                if (security.Settings.Enabled)
                {
                    RaiseIntrumentationEvent(security, HttpContext.Current, span, controllerContext.RouteData);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        /// <summary>
        /// Raising instrumentation event
        /// </summary>
        /// <param name="security">security></param>
        /// <param name="context">context</param>
        /// <param name="relatedSpan">related span</param>
        /// <param name="routeDatas">routeDatas</param>
        internal static void RaiseIntrumentationEvent(IDatadogSecurity security, HttpContext context, Span relatedSpan, RouteData routeDatas)
        {
            try
            {
                var dic = context.Request.PrepareArgsForWaf(routeDatas);
                security.InstrumentationGateway.RaiseEvent(dic, new HttpTransport(context), relatedSpan);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred raising instrumentation event");
            }
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
                    scope = CreateScope(controllerContext.DuckCast<ControllerContextStruct>());
                    HttpContext.Current.Items[HttpContextKey] = scope;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error instrumenting method {MethodName}", "System.Web.Mvc.Async.IAsyncActionInvoker.BeginInvokeAction()");
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
                Log.Error(ex, "Error instrumenting method {MethodName}", $"{AsyncActionInvokerTypeName}.EndInvokeAction()");
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
                var result = instrumentedMethod(asyncControllerActionInvoker, asyncResult);

                if (scope != null)
                {
                    HttpContextHelper.AddHeaderTagsFromHttpResponse(httpContext, scope);
                    scope.Span.SetHttpStatusCode(httpContext.Response.StatusCode, isServer: true);
                    scope.Dispose();
                }

                return result;
            }
            catch (Exception ex)
            {
                if (scope != null)
                {
                    scope.Span.SetException(ex);

                    if (httpContext != null)
                    {
                        // We don't know how long it'll take for ASP.NET to invoke the callback,
                        // so we store the real finish time
                        var now = scope.Span.Context.TraceContext.UtcNow;
                        httpContext.AddOnRequestCompleted(h => OnRequestCompleted(h, scope, now));
                    }
                    else
                    {
                        scope.Dispose();
                    }
                }

                throw;
            }
        }

        private static void OnRequestCompleted(HttpContext httpContext, Scope scope, DateTimeOffset finishTime)
        {
            HttpContextHelper.AddHeaderTagsFromHttpResponse(httpContext, scope);
            scope.Span.SetHttpStatusCode(httpContext.Response.StatusCode, isServer: true);
            scope.Span.Finish(finishTime);
            scope.Dispose();
        }
    }
}

#endif
