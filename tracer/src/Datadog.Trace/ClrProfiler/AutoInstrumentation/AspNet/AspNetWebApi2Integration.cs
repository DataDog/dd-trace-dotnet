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

        // The HttpRequestMessage property under which the OWIN hosts store the request's IOwinContext
        private const string OwinContextKey = "MS_OwinContext";

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
                        if (request.Properties.TryGetValue(OwinContextKey, out var owinContextObj))
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
                UpdateWebApiSpan(controllerContext, scope.Span, tags);

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
        internal static bool ShouldReuseActiveServerSpan(Tracer tracer)
            => tracer.Settings.OtelSemanticsEnabled
            && tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId)
            && HttpSemanticConventions.GetActiveHttpServerSpan(tracer) is not null;

        /// <summary>
        /// Records the route of the executing Web API action on the active HTTP server span and derives
        /// the span's resource name from it. Called both when the action starts and when it ends, because
        /// the route is not always resolved by the time the action starts.
        /// <para>
        /// Only applies when OpenTelemetry semantics are enabled. It differs from
        /// <see cref="UpdateWebApiSpan(IHttpControllerContext, Span, AspNetTags)"/>, which decorates the
        /// <c>aspnet-webapi.request</c> span that this integration owns with the full set of HTTP and
        /// ASP.NET tags -- this method only updates the span generated by the ASP.NET integration with
        /// route information.
        /// </para>
        /// <para>
        /// Note: This also has the side effect of updating the appropriate HttpContext key so the ASP.NET
        /// instrumentation can retrieve the calculated resource name.
        /// </para>
        /// </summary>
        /// <param name="tracer">The tracer whose active scope holds the server span</param>
        /// <param name="controllerContext">The context of the executing action</param>
        internal static void SetRouteOnActiveServerSpan(Tracer tracer, IHttpControllerContext controllerContext)
        {
            if (!tracer.Settings.OtelSemanticsEnabled)
            {
                return;
            }

            try
            {
                var serverSpan = HttpSemanticConventions.GetActiveHttpServerSpan(tracer);
                var httpContext = System.Web.HttpContext.Current;

                if (serverSpan is null && httpContext is null)
                {
                    return;
                }

                var route = GetRouteTemplate(controllerContext);
                var requestMethod = HttpSemanticConventions.NormalizeRequestMethod(controllerContext.Request?.Method.Method);
                var resourceName = HttpSemanticConventions.GetServerResourceName(requestMethod, route);

                if (serverSpan is not null)
                {
                    HttpSemanticConventions.SetHttpRoute(serverSpan, route);
                    serverSpan.ResourceName = resourceName;
                }

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

        /// <summary>
        /// Sets the full set of ASP.NET and Web API information on the Web API span generated by this integration.
        /// </summary>
        /// <param name="controllerContext">The context of the executing action</param>
        /// <param name="span">The Web API span to be updated.</param>
        /// <param name="tags">The tags object to be updated.</param>
        internal static void UpdateWebApiSpan(IHttpControllerContext controllerContext, Span span, AspNetTags tags)
        {
            try
            {
                var tracer = Tracer.Instance;
                var tracerSettings = tracer.Settings;
                var otelSemanticsEnabled = tracerSettings.OtelSemanticsEnabled;
                var newResourceNamesEnabled = tracerSettings.RouteTemplateResourceNamesEnabled;
                var request = controllerContext.Request;
                Uri requestUri = request.RequestUri;

                string method = request.Method.Method?.ToUpperInvariant() ?? "GET";
                string route = GetRouteTemplate(controllerContext);

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
                    HttpSemanticConventions.SetHttpServerRequestValues(
                        span,
                        tags,
                        resourceName: HttpSemanticConventions.GetServerResourceName(request.Method.Method, route),
                        originalMethod: request.Method.Method,
                        userAgent: request.Headers.UserAgent?.ToString(),
                        protocol: GetCurrentRequestProtocol(request),
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

        /// <summary>
        /// Gets the protocol the request arrived over, e.g. "HTTP/1.1", or <c>null</c> if it cannot be
        /// determined. Note that <c>HttpRequestMessage.Version</c> must not be used for this: neither
        /// host populates it from the incoming request, so it always holds the default of 1.1.
        /// </summary>
        /// <param name="request">The request being traced</param>
        private static string GetCurrentRequestProtocol(IHttpRequestMessage request)
        {
            // When Web API is hosted by ASP.NET, the System.Web request knows the protocol.
            if (System.Web.HttpContext.Current?.Request is { } currentRequest)
            {
                return RequestDataHelper.GetServerProtocol(currentRequest);
            }

            // A self-hosted (OWIN) Web API has no System.Web request, but the OWIN request has the protocol.
            if (request.Properties.TryGetValue(OwinContextKey, out var owinContextObj) && owinContextObj is not null)
            {
                var owinContext = owinContextObj.DuckCast<OwinContextStruct>();

                try
                {
                    return owinContext.Request.Protocol;
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Error reading the protocol from the OWIN request.");
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the template of the route that matched the request, or <c>null</c> if it cannot be
        /// determined. Callers must handle a missing route, which is represented by a null return value.
        /// </summary>
        /// <param name="controllerContext">The context of the executing action</param>
        private static string GetRouteTemplate(IHttpControllerContext controllerContext)
        {
            try
            {
                return controllerContext.RouteData?.Route?.RouteTemplate;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error reading the route template of the executing action.");
                return null;
            }
        }
    }
}
#endif
