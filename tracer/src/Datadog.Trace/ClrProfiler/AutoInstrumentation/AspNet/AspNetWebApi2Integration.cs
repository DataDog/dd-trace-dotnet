// <copyright file="AspNetWebApi2Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Datadog.Trace.AspNet;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// Contains instrumentation wrappers for ASP.NET Web API 5.
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AspNetWebApi2Integration
    {
        private const string OperationName = "aspnet-webapi.request";

        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.AspNetWebApi2));
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

                if (request != null && tracer.ActiveScope == null)
                {
                    try
                    {
                        // extract propagated http headers
                        var headers = request.Headers;
                        var headersCollection = new HttpHeadersCollection(headers);

                        propagatedContext = SpanContextPropagator.Instance.Extract(headersCollection);
                        tagsFromHeaders = SpanContextPropagator.Instance.ExtractHeaderTags(headersCollection, tracer.Settings.HeaderTags, SpanContextPropagator.HttpRequestHeadersTagPrefix);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error extracting propagated HTTP headers.");
                    }
                }

                tags = new AspNetTags();
                scope = tracer.StartActiveWithTags(OperationName, propagatedContext, tags: tags);
                UpdateSpan(controllerContext, scope.Span, tags, tagsFromHeaders);

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: true);
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
                var newResourceNamesEnabled = Tracer.Instance.Settings.RouteTemplateResourceNamesEnabled;
                var request = controllerContext.Request;
                Uri requestUri = request.RequestUri;

                string host = request.Headers.Host ?? string.Empty;
                string rawUrl = requestUri?.ToString().ToLowerInvariant() ?? string.Empty;
                string method = request.Method.Method?.ToUpperInvariant() ?? "GET";
                string route = null;
                try
                {
                    route = controllerContext.RouteData.Route.RouteTemplate;
                }
                catch
                {
                }

                string resourceName;

                if (route != null)
                {
                    resourceName = $"{method} {(newResourceNamesEnabled ? "/" : string.Empty)}{route.ToLowerInvariant()}";
                }
                else if (requestUri != null)
                {
                    var cleanUri = UriHelpers.GetCleanUriPath(requestUri);
                    resourceName = $"{method} {cleanUri.ToLowerInvariant()}";
                }
                else
                {
                    resourceName = $"{method}";
                }

                string controller = string.Empty;
                string action = string.Empty;
                string area = string.Empty;
                try
                {
                    var routeValues = controllerContext.RouteData.Values;
                    if (routeValues != null)
                    {
                        controller = (routeValues.GetValueOrDefault("controller") as string)?.ToLowerInvariant();
                        action = (routeValues.GetValueOrDefault("action") as string)?.ToLowerInvariant();
                        area = (routeValues.GetValueOrDefault("area") as string)?.ToLowerInvariant();
                    }
                }
                catch
                {
                }

                // Replace well-known routing tokens
                resourceName =
                    resourceName
                       .Replace("{area}", area)
                       .Replace("{controller}", controller)
                       .Replace("{action}", action);

                span.DecorateWebServerSpan(
                    resourceName: resourceName,
                    method: method,
                    host: host,
                    httpUrl: rawUrl,
                    tags,
                    headerTags);

                tags.AspNetAction = action;
                tags.AspNetController = controller;
                tags.AspNetArea = area;
                tags.AspNetRoute = route;

                if (newResourceNamesEnabled)
                {
                    // set the resource name in the HttpContext so TracingHttpModule can update root span
                    var httpContext = System.Web.HttpContext.Current;
                    if (httpContext is not null)
                    {
                        httpContext.Items[SharedConstants.HttpContextPropagatedResourceNameKey] = resourceName;
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
