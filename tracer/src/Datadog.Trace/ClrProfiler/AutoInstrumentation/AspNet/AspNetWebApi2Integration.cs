// <copyright file="AspNetWebApi2Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Datadog.Trace.AppSec;
using Datadog.Trace.AspNet;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.OpenTelemetry;
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
                var tracer = Tracer.Instance;
                if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
                {
                    // integration disabled, don't create a scope, skip this trace
                    return null;
                }

                var request = controllerContext.Request;
                PropagationContext extractedContext = default;
                HttpHeadersCollection? headersCollection = null;
                tags = new AspNetTags();

                if (request != null && tracer.InternalActiveScope == null)
                {
                    try
                    {
                        // extract propagated http headers
                        headersCollection = new HttpHeadersCollection(request.Headers);
                        extractedContext = tracer.TracerManager.SpanContextPropagator.Extract(headersCollection.Value).MergeBaggageInto(Baggage.Current);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error extracting propagated HTTP headers.");
                    }

                    if (tracer.Settings.IpHeaderEnabled || Security.Instance.AppsecEnabled)
                    {
                        const string httpContextKey = "MS_HttpContext";
                        if (request.Properties.TryGetValue("MS_OwinContext", out var owinContextObj))
                        {
                            if (owinContextObj != null)
                            {
                                var owinContext = owinContextObj.DuckCast<OwinContextStruct>();
                                Headers.Ip.RequestIpExtractor.AddIpToTags(
                                    owinContext.Request.RemoteIpAddress,
                                    owinContext.Request.IsSecure,
                                    key => request.Headers.TryGetValues(key, out var values) ? values?.FirstOrDefault() : string.Empty,
                                    tracer.Settings.IpHeader,
                                    tags);
                            }
                        }
                        else if (request.Properties.TryGetValue(httpContextKey, out var property))
                        {
                            if (property is HttpContextWrapper objectCtx)
                            {
                                Headers.Ip.RequestIpExtractor.AddIpToTags(
                                    objectCtx.Request.UserHostAddress,
                                    objectCtx.Request.IsSecureConnection,
                                    key => request.Headers.TryGetValues(key, out var values) ? values?.FirstOrDefault() : string.Empty,
                                    tracer.Settings.IpHeader,
                                    tags);
                            }
                        }
                    }
                }

                scope = tracer.StartActiveInternal(OperationName, extractedContext.SpanContext, tags: tags);
                UpdateSpan(controllerContext, scope.Span, tags);

                if (headersCollection is not null)
                {
                    tracer.TracerManager.SpanContextPropagator.AddHeadersToSpanAsTags(scope.Span, headersCollection.Value, tracer.CurrentTraceSettings.Settings.HeaderTags, SpanContextPropagator.HttpRequestHeadersTagPrefix, request.Headers.UserAgent.ToString());
                    tracer.TracerManager.SpanContextPropagator.AddSecurityTestingHeadersAsTags(scope.Span, headersCollection.Value);
                }

                tracer.TracerManager.SpanContextPropagator.AddBaggageToSpanAsTags(scope.Span, extractedContext.Baggage, tracer.Settings.BaggageTagKeys);

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.CurrentTraceSettings.Settings, enabledWithGlobalSetting: true);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating scope.");
            }

            return scope;
        }

        /// <summary>
        /// Determines whether this integration must enrich an existing HTTP server span rather than
        /// create one of its own. The OpenTelemetry HTTP semantic conventions describe a single server
        /// span per request, so when Web API is hosted by another instrumented framework (i.e. ASP.NET,
        /// as opposed to being self-hosted with OWIN) that framework's span is the one to use.
        /// </summary>
        /// <param name="tracer">The tracer whose active scope is inspected</param>
        internal static bool UsesExistingServerSpan(Tracer tracer)
            => tracer.Settings.OtelSemanticsEnabled
            && tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId)
            && HttpSemanticConventions.GetActiveHttpServerSpan(tracer) is not null;

        /// <summary>
        /// Applies the route information of the executing Web API action to the existing HTTP server
        /// span. Called both when the action starts and when it ends, because the route is not always
        /// resolved by the time the action starts.
        /// </summary>
        /// <param name="tracer">The tracer whose active scope holds the server span</param>
        /// <param name="controllerContext">The context of the executing action</param>
        internal static void UpdateExistingServerSpan(Tracer tracer, IHttpControllerContext controllerContext)
        {
            try
            {
                var route = TryGetRouteTemplate(controllerContext);
                var requestMethod = HttpSemanticConventions.NormalizeRequestMethod(controllerContext.Request?.Method.Method);
                var resourceName = HttpSemanticConventions.GetServerResourceName(requestMethod, route);

                if (HttpSemanticConventions.GetActiveHttpServerSpan(tracer) is { } serverSpan)
                {
                    HttpSemanticConventions.SetHttpRoute(serverSpan, route);
                    serverSpan.ResourceName = resourceName;
                }

                // Also record it in the HttpContext so TracingHttpModule applies it when the request
                // ends, which is the only way to reach the span when the pipeline unwinds elsewhere.
                var httpContext = System.Web.HttpContext.Current;
                if (httpContext is not null)
                {
                    httpContext.Items[SharedItems.HttpContextPropagatedResourceNameKey] = resourceName;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating the ASP.NET server span with Web API route data.");
            }
        }

        internal static void UpdateSpan(IHttpControllerContext controllerContext, Span span, AspNetTags tags)
        {
            try
            {
                var tracer = Tracer.Instance;
                var tracerSettings = tracer.Settings;
                var otelSemanticsEnabled = tracerSettings.OtelSemanticsEnabled;
                var newResourceNamesEnabled = tracerSettings.RouteTemplateResourceNamesEnabled || otelSemanticsEnabled;
                var request = controllerContext.Request;
                Uri requestUri = request.RequestUri;

                string method = request.Method.Method?.ToUpperInvariant() ?? "GET";
                string route = TryGetRouteTemplate(controllerContext);

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
                        addSlashPrefix: newResourceNamesEnabled,
                        expandRouteTemplates: newResourceNamesEnabled && tracer.Settings.ExpandRouteTemplatesEnabled);
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
                    }
                    catch
                    {
                    }
                }

                if (otelSemanticsEnabled)
                {
                    // HttpRequestMessage.Version is not the protocol the request arrived over: the
                    // System.Web host builds the message itself and leaves the default 1.1 in place,
                    // so read the protocol from the underlying request instead. A self-hosted Web API
                    // has no System.Web request, and no protocol worth guessing at.
                    var currentRequest = System.Web.HttpContext.Current?.Request;

                    HttpSemanticConventions.SetHttpServerRequestValues(
                        span,
                        tags,
                        resourceName: HttpSemanticConventions.GetServerResourceName(request.Method.Method, route),
                        originalMethod: request.Method.Method,
                        userAgent: request.Headers.UserAgent?.ToString(),
                        protocol: currentRequest is null ? null : RequestDataHelper.GetServerProtocol(currentRequest),
                        hostHeader: request.Headers.Host,
                        requestUri: requestUri,
                        queryStringManager: tracer.TracerManager.QueryStringManager);
                }
                else
                {
                    string host = request.Headers.Host ?? string.Empty;
                    var url = request.GetUrlForSpan(tracer.TracerManager.QueryStringManager);
                    var userAgent = request.Headers.UserAgent?.ToString() ?? string.Empty;

                    span.DecorateWebServerSpan(
                        resourceName: resourceName,
                        method: method,
                        host: host,
                        httpUrl: url,
                        userAgent: userAgent,
                        tags);
                }

                if (tags is not null)
                {
                    tags.AspNetAction = action;
                    tags.AspNetController = controller;
                    tags.AspNetArea = area;
                    tags.AspNetRoute = route;
                    if (span.Context.TraceContext.RootSpan.Tags is AspNetTags rootAspNetTags)
                    {
                        rootAspNetTags.HttpRoute = route;
                    }
                    else
                    {
                        span.Context.TraceContext.RootSpan?.SetTag(Tags.HttpRoute, route);
                    }
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

        private static string TryGetRouteTemplate(IHttpControllerContext controllerContext)
        {
            try
            {
                return controllerContext.RouteData.Route.RouteTemplate;
            }
            catch
            {
                return null;
            }
        }
    }
}
#endif
