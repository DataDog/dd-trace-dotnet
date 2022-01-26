// <copyright file="AspNetCoreOnFrameworkHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    internal static class AspNetCoreOnFrameworkHelpers
    {
        private const IntegrationId IntegrationId = Configuration.IntegrationId.AspNetCore;
        private const string HttpRequestInOperationName = "aspnet_core.request";
        private const string MvcOperationName = "aspnet_core_mvc.request";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HostingApplication_ProcessRequestAsync_Integration));

        public static string GetDefaultResourceName(IHttpRequest request)
        {
            string httpMethod = request.Method?.ToUpperInvariant() ?? "UNKNOWN";

            string absolutePath = request.PathBase.HasValue
                                      ? request.PathBase.ToUriComponent() + request.Path.ToUriComponent()
                                      : request.Path.ToUriComponent();

            string resourceUrl = UriHelpers.GetCleanUriPath(absolutePath)
                                           .ToLowerInvariant();

            return $"{httpMethod} {resourceUrl}";
        }

        public static Scope StartAspNetCorePipelineScope(Tracer tracer, IHttpContext httpContext, IHttpRequest request, string resourceName)
        {
            // string host = request.Host.Value;
            string host = request.Host.Value;
            string httpMethod = request.Method?.ToUpperInvariant() ?? "UNKNOWN";
            string url = GetUrl(request);
            resourceName ??= GetDefaultResourceName(request);

            SpanContext propagatedContext = ExtractPropagatedContext(request);
            var tagsFromHeaders = ExtractHeaderTags(request, tracer);

            AspNetCoreTags tags;

            if (tracer.Settings.RouteTemplateResourceNamesEnabled)
            {
                // var originalPath = request.PathBase.HasValue ? request.PathBase.Add(request.Path) : request.Path;
                // httpContext.Features.Set(new RequestTrackingFeature(originalPath));
                tags = new AspNetCoreEndpointTags();
            }
            else
            {
                tags = new AspNetCoreTags();
            }

            var scope = tracer.StartActiveInternal(HttpRequestInOperationName, propagatedContext, tags: tags);

            scope.Span.DecorateWebServerSpan(resourceName, httpMethod, host, url, tags, tagsFromHeaders);

            tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: true);

            return scope;
        }

        private static SpanContext ExtractPropagatedContext(IHttpRequest request)
        {
            try
            {
                // extract propagation details from http headers
                var requestHeaders = request.Headers;

                if (requestHeaders != null)
                {
                    return SpanContextPropagator.Instance.Extract(new IHeaderDictionaryHeadersCollection(requestHeaders));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting propagated HTTP headers.");
            }

            return null;
        }

        private static IEnumerable<KeyValuePair<string, string>> ExtractHeaderTags(IHttpRequest request, Tracer tracer)
        {
            var settings = tracer.Settings;

            if (!settings.HeaderTags.IsNullOrEmpty())
            {
                try
                {
                    // extract propagation details from http headers
                    var requestHeaders = request.Headers;

                    if (requestHeaders != null)
                    {
                        return SpanContextPropagator.Instance.ExtractHeaderTags(new IHeaderDictionaryHeadersCollection(requestHeaders), settings.HeaderTags, defaultTagPrefix: SpanContextPropagator.HttpRequestHeadersTagPrefix);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error extracting propagated HTTP headers.");
                }
            }

            return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        private static string GetUrl(IHttpRequest request)
        {
            if (request.Host.HasValue)
            {
                return $"{request.Scheme}://{request.Host.Value}{request.PathBase.ToUriComponent()}{request.Path.ToUriComponent()}";
            }

            // HTTP 1.0 requests are not required to provide a Host to be valid
            // Since this is just for display, we can provide a string that is
            // not an actual Uri with only the fields that are specified.
            // request.GetDisplayUrl(), used above, will throw an exception
            // if request.Host is null.
            return $"{request.Scheme}://{HttpRequestExtensions.NoHostSpecified}{request.PathBase.ToUriComponent()}{request.Path.ToUriComponent()}";
        }

        /*
        private static Span StartMvcCoreSpan(Tracer tracer, Span parentSpan, BeforeActionStruct typedArg, HttpContext httpContext, HttpRequest request)
        {
            // Create a child span for the MVC action
            var mvcSpanTags = new AspNetCoreMvcTags();
            var mvcScope = tracer.StartActiveInternal(MvcOperationName, parentSpan.Context, tags: mvcSpanTags);
            var span = mvcScope.Span;
            span.Type = SpanTypes.Web;

            // This is only called with new route names, so parent tags are always AspNetCoreEndpointTags
            var parentTags = (AspNetCoreEndpointTags)parentSpan.Tags;

            var trackingFeature = httpContext.Features.Get<AspNetCoreHttpRequestHandler.RequestTrackingFeature>();
            var isUsingEndpointRouting = trackingFeature.IsUsingEndpointRouting;

            var isFirstExecution = trackingFeature.IsFirstPipelineExecution;
            if (isFirstExecution)
            {
                trackingFeature.IsFirstPipelineExecution = false;
                if (!trackingFeature.MatchesOriginalPath(httpContext.Request))
                {
                    // URL has changed from original, so treat this execution as a "subsequent" request
                    // Typically occurs for 404s for example
                    isFirstExecution = false;
                }
            }

            ActionDescriptor actionDescriptor = typedArg.ActionDescriptor;
            IDictionary<string, string> routeValues = actionDescriptor.RouteValues;

            string controllerName = routeValues.TryGetValue("controller", out controllerName)
                ? controllerName?.ToLowerInvariant()
                : null;
            string actionName = routeValues.TryGetValue("action", out actionName)
                ? actionName?.ToLowerInvariant()
                : null;
            string areaName = routeValues.TryGetValue("area", out areaName)
                ? areaName?.ToLowerInvariant()
                : null;
            string pagePath = routeValues.TryGetValue("page", out pagePath)
                ? pagePath?.ToLowerInvariant()
                : null;
            string aspNetRoute = trackingFeature.Route;
            string resourceName = trackingFeature.ResourceName;

            if (aspNetRoute is null || resourceName is null)
            {
                // Not using endpoint routing
                string rawRouteTemplate = actionDescriptor.AttributeRouteInfo?.Template;
                RouteTemplate routeTemplate = null;
                if (rawRouteTemplate is not null)
                {
                    try
                    {
                        routeTemplate = TemplateParser.Parse(rawRouteTemplate);
                    }
                    catch { }
                }

                if (routeTemplate is null)
                {
                    var routeData = httpContext.Features.Get<IRoutingFeature>()?.RouteData;
                    if (routeData is not null)
                    {
                        var route = routeData.Routers.OfType<RouteBase>().FirstOrDefault();
                        routeTemplate = route?.ParsedTemplate;
                    }
                }

                if (routeTemplate is not null)
                {
                    // If we have a route, overwrite the existing resource name
                    var resourcePathName = SimplifyRoutePattern(
                        routeTemplate,
                        routeValues,
                        areaName: areaName,
                        controllerName: controllerName,
                        actionName: actionName);

                    resourceName = $"{parentTags.HttpMethod} {request.PathBase.ToUriComponent()}{resourcePathName}";
                    aspNetRoute = routeTemplate?.TemplateText.ToLowerInvariant();
                }
            }

            // mirror the parent if we couldn't extract a route for some reason
            // (and the parent is not using the placeholder resource name)
            span.ResourceName = resourceName
                             ?? (string.IsNullOrEmpty(parentSpan.ResourceName)
                                     ? AspNetCoreRequestHandler.GetDefaultResourceName(httpContext.Request)
                                     : parentSpan.ResourceName);

            mvcSpanTags.AspNetCoreAction = actionName;
            mvcSpanTags.AspNetCoreController = controllerName;
            mvcSpanTags.AspNetCoreArea = areaName;
            mvcSpanTags.AspNetCorePage = pagePath;
            mvcSpanTags.AspNetCoreRoute = aspNetRoute;

            if (!isUsingEndpointRouting && isFirstExecution)
            {
                // If we're using endpoint routing or this is a pipeline re-execution,
                // these will already be set correctly
                parentTags.AspNetCoreRoute = aspNetRoute;
                parentSpan.ResourceName = span.ResourceName;
            }

            return span;
        }
        */
    }
}
#endif
