// <copyright file="AspNetWebApi2Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AspNet;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// Contains instrumentation wrappers for ASP.NET Web API 5.
    /// </summary>
    internal static class AspNetWebApi2Integration
    {
        internal const string HttpContextKey = "__Datadog.Trace.ClrProfiler.Integrations.AspNetWebApi2Integration";

        private const string OperationName = "aspnet-webapi.request";

        private const IntegrationId IntegrationId = Configuration.IntegrationId.AspNetWebApi2;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AspNetWebApi2Integration));

        internal static Scope CreateScope(IHttpControllerContext controllerContext, out AspNetTags tags)
        {
            Scope scope = null;
            tags = null;

            try
            {
                if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId))
                {
                    // integration disabled, don't create a scope, skip this trace
                    return null;
                }

                var tracer = Tracer.Instance;
                var request = controllerContext.Request;
                SpanContext propagatedContext = null;
                var tagsFromHeaders = Enumerable.Empty<KeyValuePair<string, string>>();
                tags = new AspNetTags();

                if (request != null && tracer.InternalActiveScope == null)
                {
                    try
                    {
                        // extract propagated http headers
                        var headers = request.Headers;
                        var headersCollection = new HttpHeadersCollection(headers);

                        propagatedContext = SpanContextPropagator.Instance.Extract(headersCollection);
                        tagsFromHeaders = SpanContextPropagator.Instance.ExtractHeaderTags(headersCollection, tracer.Settings.HeaderTags, SpanContextPropagator.HttpRequestHeadersTagPrefix, request.Headers.UserAgent.ToString());
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error extracting propagated HTTP headers.");
                    }

                    if (!tracer.Settings.IpHeaderDisabled)
                    {
                        if (request.Properties.TryGetValue("MS_OwinContext", out var owinContextObj))
                        {
                            if (owinContextObj != null)
                            {
                                if (owinContextObj.TryDuckCast<IOwinContext>(out var owinContext))
                                {
                                    Headers.Ip.RequestIpExtractor.AddIpToTags(
                                        owinContext.Request.RemoteIpAddress,
                                        owinContext.Request.IsSecure,
                                        key => request.Headers.TryGetValues(key, out var values) ? values?.FirstOrDefault() : string.Empty,
                                        tracer.Settings.IpHeader,
                                        tags);
                                }
                            }
                        }
                    }
                }

                scope = tracer.StartActiveInternal(OperationName, propagatedContext, tags: tags);
                UpdateSpan(controllerContext, scope.Span, tags, tagsFromHeaders);
                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: true);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating scope.");
            }

            return scope;
        }

        internal static void UpdateSpan(IHttpControllerContext controllerContext, Span span, AspNetTags tags, IEnumerable<KeyValuePair<string, string>> headerTags)
        {
            try
            {
                var tracerSettings = Tracer.Instance.Settings;
                var newResourceNamesEnabled = tracerSettings.RouteTemplateResourceNamesEnabled;
                var request = controllerContext.Request;
                Uri requestUri = request.RequestUri;

                string host = request.Headers.Host ?? string.Empty;
                var userAgent = request.Headers.UserAgent?.ToString() ?? string.Empty;
                string method = request.Method.Method?.ToUpperInvariant() ?? "GET";
                string route = null;
                try
                {
                    route = controllerContext.RouteData.Route.RouteTemplate;
                }
                catch
                {
                }

                IDictionary<string, object> routeValues = null;
                try
                {
                    routeValues = controllerContext.RouteData.Values;
                }
                catch
                {
                }

                string resourceName;

                string controller = string.Empty;
                string action = string.Empty;
                string area = string.Empty;
                string resolvedRoute = string.Empty;
                if (route is not null && routeValues is not null)
                {
                    resourceName = AspNetResourceNameHelper.CalculateResourceName(
                        httpMethod: method,
                        routeTemplate: route,
                        routeValues,
                        defaults: null,
                        out area,
                        out controller,
                        out action,
                        out resolvedRoute,
                        addSlashPrefix: newResourceNamesEnabled,
                        expandRouteTemplates: newResourceNamesEnabled && Tracer.Instance.Settings.ExpandRouteTemplatesEnabled);
                }
                else if (requestUri != null)
                {
                    var cleanUri = UriHelpers.GetCleanUriPath(requestUri, controllerContext.RequestContext.VirtualPathRoot);
                    resourceName = $"{method} {cleanUri}";
                }
                else
                {
                    resourceName = method;
                }

                if (route is null && routeValues is not null)
                {
                    // we weren't able to get the route template (somehow) but _were_ able to
                    // get the route values. Not sure how this is possible, but is preexisting behaviour
                    try
                    {
                        area = (routeValues.GetValueOrDefault("area") as string)?.ToLowerInvariant();
                        controller = (routeValues.GetValueOrDefault("controller") as string)?.ToLowerInvariant();
                        action = (routeValues.GetValueOrDefault("action") as string)?.ToLowerInvariant();
                        resolvedRoute = $"{area}/{controller}/{action}";
                    }
                    catch
                    {
                    }
                }

                var url = request.GetUrlWithQueryString(tracerSettings.EnableQueryStringReporting);

                span.DecorateWebServerSpan(
                    resourceName: resourceName,
                    method: method,
                    host: host,
                    httpUrl: url,
                    userAgent: userAgent,
                    tags,
                    headerTags);

                if (tags is not null)
                {
                    tags.AspNetAction = action;
                    tags.AspNetController = controller;
                    tags.AspNetArea = area;
                    tags.AspNetRoute = route;
                    span.Context.TraceContext.RootSpan?.SetTag(Tags.HttpRoute, route);
                }

                if (newResourceNamesEnabled)
                {
                    // set the resource name in the HttpContext so TracingHttpModule can update root span
                    var httpContext = System.Web.HttpContext.Current;
                    if (httpContext is not null)
                    {
                        httpContext.Items[SharedItems.HttpContextPropagatedResourceNameKey] = resourceName;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error populating scope data.");
            }
        }
    }
}
#endif
