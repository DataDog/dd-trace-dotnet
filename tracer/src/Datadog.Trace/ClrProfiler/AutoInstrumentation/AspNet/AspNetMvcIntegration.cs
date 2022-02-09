// <copyright file="AspNetMvcIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Routing;
using Datadog.Trace.AspNet;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// The ASP.NET MVC integration.
    /// </summary>
    internal static class AspNetMvcIntegration
    {
        internal const string HttpContextKey = "__Datadog.Trace.ClrProfiler.Integrations.AspNetMvcIntegration";

        private const string OperationName = "aspnet-mvc.request";
        private const string ChildActionOperationName = "aspnet-mvc.request.child-action";

        private const string RouteCollectionRouteTypeName = "System.Web.Mvc.Routing.RouteCollectionRoute";

        private const IntegrationId IntegrationId = Configuration.IntegrationId.AspNetMvc;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AspNetMvcIntegration));

        /// <summary>
        /// Creates a scope used to instrument an MVC action and populates some common details.
        /// </summary>
        /// <param name="controllerContext">The System.Web.Mvc.ControllerContext that was passed as an argument to the instrumented method.</param>
        /// <returns>A new scope used to instrument an MVC action.</returns>
        internal static Scope CreateScope(ControllerContextStruct controllerContext)
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

                if (Tracer.Instance?.Settings.IsIntegrationEnabled(IntegrationId) is false)
                {
                    Log.Debug(Tracer.Instance is null ? "Tracer.Instance is null." : "AspNet Integration is disabled.");
                    return null;
                }

                // integration enabled, go create a scope!
                var newResourceNamesEnabled = Tracer.Instance.Settings.RouteTemplateResourceNamesEnabled;
                string host = httpContext.Request.Headers.Get("Host");
                string httpMethod = httpContext.Request.HttpMethod.ToUpperInvariant();
                string url = httpContext.Request.RawUrl.ToLowerInvariant();
                string resourceName = null;

                RouteData routeData = controllerContext.RouteData;
                Route route = routeData?.Route as Route;
                RouteValueDictionary routeValues = routeData?.Values;
                bool wasAttributeRouted = false;
                bool isChildAction = controllerContext.ParentActionViewContext.RouteData?.Values["controller"] is not null;

                if (isChildAction && newResourceNamesEnabled)
                {
                    // For child actions, we want to stick to what was requested in the http request.
                    // And the child action being a child, then we have already computed the resourcename.
                    resourceName = httpContext.Items[SharedItems.HttpContextPropagatedResourceNameKey] as string;
                }

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

                if (tracer.InternalActiveScope == null)
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
                scope = Tracer.Instance.StartActiveInternal(isChildAction ? ChildActionOperationName : OperationName, propagatedContext, tags: tags);
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

                if (newResourceNamesEnabled && string.IsNullOrEmpty(httpContext.Items[SharedItems.HttpContextPropagatedResourceNameKey] as string))
                {
                    // set the resource name in the HttpContext so TracingHttpModule can update root span
                    httpContext.Items[SharedItems.HttpContextPropagatedResourceNameKey] = resourceName;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }
    }
}
#endif
