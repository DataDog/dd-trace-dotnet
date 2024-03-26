// <copyright file="AspNetMvcIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Routing;
using Datadog.Trace.AppSec;
using Datadog.Trace.AspNet;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Http;

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
                // integration enabled, go create a scope!
                var tracer = Tracer.Instance;
                if (tracer.Settings.IsIntegrationEnabled(IntegrationId))
                {
                    var newResourceNamesEnabled = tracer.Settings.RouteTemplateResourceNamesEnabled;
                    string host = httpContext.Request.Headers.Get("Host");
                    var userAgent = httpContext.Request.Headers.Get(HttpHeaderNames.UserAgent);
                    string httpMethod = httpContext.Request.HttpMethod.ToUpperInvariant();
                    var url = httpContext.Request.GetUrlForSpan(tracer.TracerManager.QueryStringManager);
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
                        }
                    }

                    string routeUrl = route?.Url;
                    string areaName;
                    string controllerName;
                    string actionName;
                    if ((wasAttributeRouted || newResourceNamesEnabled) && string.IsNullOrEmpty(resourceName) && !string.IsNullOrEmpty(routeUrl))
                    {
                        resourceName = AspNetResourceNameHelper.CalculateResourceName(
                            httpMethod: httpMethod,
                            routeTemplate: routeUrl,
                            routeValues,
                            defaults: wasAttributeRouted ? null : route.Defaults,
                            out areaName,
                            out controllerName,
                            out actionName,
                            expandRouteTemplates: newResourceNamesEnabled && tracer.Settings.ExpandRouteTemplatesEnabled);
                    }
                    else
                    {
                        // just grab area/controller/action directly
                        areaName = (routeValues?.GetValueOrDefault("area") as string)?.ToLowerInvariant();
                        controllerName = (routeValues?.GetValueOrDefault("controller") as string)?.ToLowerInvariant();
                        actionName = (routeValues?.GetValueOrDefault("action") as string)?.ToLowerInvariant();
                    }

                    if (string.IsNullOrEmpty(resourceName) && httpContext.Request.Url != null)
                    {
                        var cleanUri = UriHelpers.GetCleanUriPath(httpContext.Request.Url, httpContext.Request.ApplicationPath);
                        resourceName = $"{httpMethod} {cleanUri.ToLowerInvariant()}";
                    }

                    if (string.IsNullOrEmpty(resourceName))
                    {
                        // Keep the legacy resource name, just to have something
                        resourceName = $"{httpMethod} {controllerName}.{actionName}";
                    }

                    SpanContext propagatedContext = null;
                    NameValueHeadersCollection? headers = null;

                    if (tracer.InternalActiveScope == null)
                    {
                        try
                        {
                            // extract propagated http headers
                            headers = httpContext.Request.Headers.Wrap();
                            propagatedContext = SpanContextPropagator.Instance.Extract(headers.Value);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error extracting propagated HTTP headers.");
                        }
                    }

                    var tags = new AspNetTags();
                    scope = tracer.StartActiveInternal(isChildAction ? ChildActionOperationName : OperationName, propagatedContext, tags: tags);
                    span = scope.Span;

                    span.DecorateWebServerSpan(
                        resourceName: resourceName,
                        method: httpMethod,
                        host: host,
                        httpUrl: url,
                        userAgent: userAgent,
                        tags);

                    if (headers is not null)
                    {
                        SpanContextPropagator.Instance.AddHeadersToSpanAsTags(span, headers.Value, tracer.Settings.HeaderTagsInternal, SpanContextPropagator.HttpRequestHeadersTagPrefix);
                    }

                    if (tracer.Settings.IpHeaderEnabled || Security.Instance.Enabled)
                    {
                        Headers.Ip.RequestIpExtractor.AddIpToTags(httpContext.Request.UserHostAddress, httpContext.Request.IsSecureConnection, key => httpContext.Request.Headers[key], tracer.Settings.IpHeader, tags);
                    }

                    tags.AspNetRoute = routeUrl;
                    tags.AspNetArea = areaName;
                    tags.AspNetController = controllerName;
                    tags.AspNetAction = actionName;
                    var rootspanTags = span.Context.TraceContext?.RootSpan.Tags;

                    // in case of a transfered request, the child request shouldnt set a new http route.
                    if (rootspanTags is AspNetTags rootAspNetTags)
                    {
                        if (string.IsNullOrEmpty(rootAspNetTags.HttpRoute))
                        {
                            rootAspNetTags.HttpRoute = routeUrl;
                        }
                    }
                    else if (string.IsNullOrEmpty(rootspanTags.GetTag(Tags.HttpRoute)))
                    {
                        span.Context.TraceContext?.RootSpan.Tags.SetTag(Tags.HttpRoute, routeUrl);
                    }

                    tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: true);

                    if (newResourceNamesEnabled && string.IsNullOrEmpty(httpContext.Items[SharedItems.HttpContextPropagatedResourceNameKey] as string))
                    {
                        // set the resource name in the HttpContext so TracingHttpModule can update root span
                        httpContext.Items[SharedItems.HttpContextPropagatedResourceNameKey] = resourceName;
                    }

                    tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
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
