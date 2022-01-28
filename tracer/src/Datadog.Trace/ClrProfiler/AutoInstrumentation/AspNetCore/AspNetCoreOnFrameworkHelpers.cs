// <copyright file="AspNetCoreOnFrameworkHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
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
        private static readonly Func<string, object> _createTemplateParser;
        private static readonly Type _routeBaseType;

        internal const string HttpContextAspNetCoreScopeKey = "__Datadog.Trace.AspNetCore.Scope__";

        static AspNetCoreOnFrameworkHelpers()
        {
            var templateParserType = Type.GetType("Microsoft.AspNetCore.Routing.Template.TemplateParser, Microsoft.AspNetCore.Routing", throwOnError: false);
            if (templateParserType is not null)
            {
                var parseMethod = templateParserType.GetMethod("Parse", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                _createTemplateParser = (Func<string, object>)parseMethod.CreateDelegate(typeof(Func<string, object>));
            }

            _routeBaseType = Type.GetType("Microsoft.AspNetCore.Routing.RouteBase, Microsoft.AspNetCore.Routing", throwOnError: false);
        }

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
                var originalPath = request.PathBase.HasValue ? request.PathBase.Add(request.Path.Instance).DuckCast<IPathString>() : request.Path;
                httpContext.Features.Set(new RequestTrackingFeature(originalPath));
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

        public static Scope StartMvcCoreScope<TActionContext>(Tracer tracer, Span parentSpan, TActionContext typedArg)
            where TActionContext : IActionContext
        {
            IHttpContext httpContext = typedArg.HttpContext;
            IHttpRequest request = httpContext.Request;

            // Create a child span for the MVC action
            var mvcSpanTags = new AspNetCoreMvcTags();
            var mvcScope = tracer.StartActiveInternal(MvcOperationName, parentSpan.Context, tags: mvcSpanTags);
            var span = mvcScope.Span;
            span.Type = SpanTypes.Web;

            // This is only called with new route names, so parent tags are always AspNetCoreEndpointTags
            var parentTags = (AspNetCoreEndpointTags)parentSpan.Tags;

            var trackingFeature = httpContext.Features.Get<RequestTrackingFeature>();
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

            IActionDescriptor actionDescriptor = typedArg.GetActionDescriptor();
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
                IRouteTemplate routeTemplate = null;
                if (rawRouteTemplate is not null && _createTemplateParser is not null)
                {
                    try
                    {
                        routeTemplate = _createTemplateParser(rawRouteTemplate).DuckCast<IRouteTemplate>();
                    }
                    catch { }
                }

                if (_routeBaseType is not null && routeTemplate is null)
                {
                    var routeData = httpContext.Features.GetIRoutingFeature()?.DuckCast<IRoutingFeature>().RouteData;
                    if (routeData?.Instance is not null)
                    {
                        var route = routeData.Routers.Where(r => _routeBaseType.IsAssignableFrom(r.GetType())).FirstOrDefault();
                        routeTemplate = route?.DuckCast<RouteBaseStruct>().ParsedTemplate;
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
                                     ? GetDefaultResourceName(httpContext.Request)
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

            return mvcScope;
        }

        public static void SetLegacyResourceNames<TActionContext>(TActionContext typedArg, Span span)
            where TActionContext : IActionContext
        {
            IActionDescriptor actionDescriptor = typedArg.GetActionDescriptor();
            IHttpRequest request = typedArg.HttpContext.Request;

            string httpMethod = request.Method?.ToUpperInvariant() ?? "UNKNOWN";
            string routeTemplate = actionDescriptor.AttributeRouteInfo?.Template;
            if (routeTemplate is null)
            {
                string controllerName = actionDescriptor.RouteValues["controller"];
                string actionName = actionDescriptor.RouteValues["action"];

                routeTemplate = $"{controllerName}/{actionName}";
            }

            string resourceName = $"{httpMethod} {routeTemplate}";

            // override the parent's resource name with the MVC route template
            span.ResourceName = resourceName;
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

        private static string SimplifyRoutePattern(
            IRouteTemplate routePattern,
            IDictionary<string, string> routeValueDictionary,
            string areaName,
            string controllerName,
            string actionName)
        {
            var maxSize = routePattern.TemplateText.Length
                        + (string.IsNullOrEmpty(areaName) ? 0 : Math.Max(areaName.Length - 4, 0)) // "area".Length
                        + (string.IsNullOrEmpty(controllerName) ? 0 : Math.Max(controllerName.Length - 10, 0)) // "controller".Length
                        + (string.IsNullOrEmpty(actionName) ? 0 : Math.Max(actionName.Length - 6, 0)) // "action".Length
                        + 1; // '/' prefix

            var sb = StringBuilderCache.Acquire(maxSize);

            foreach (var pathSegmentObject in routePattern.Segments)
            {
                if (pathSegmentObject.TryDuckCast<TemplateSegmentStruct>(out var pathSegment))
                {
                    foreach (var partObject in pathSegment.Parts)
                    {
                        if (partObject.TryDuckCast<TemplatePartStruct>(out var part))
                        {
                            var partName = part.Name;

                            if (!part.IsParameter)
                            {
                                sb.Append('/');
                                sb.Append(part.Text);
                            }
                            else if (partName.Equals("area", StringComparison.OrdinalIgnoreCase))
                            {
                                sb.Append('/');
                                sb.Append(areaName);
                            }
                            else if (partName.Equals("controller", StringComparison.OrdinalIgnoreCase))
                            {
                                sb.Append('/');
                                sb.Append(controllerName);
                            }
                            else if (partName.Equals("action", StringComparison.OrdinalIgnoreCase))
                            {
                                sb.Append('/');
                                sb.Append(actionName);
                            }
                            else if (!part.IsOptional || routeValueDictionary.ContainsKey(partName))
                            {
                                sb.Append("/{");
                                if (part.IsCatchAll)
                                {
                                    sb.Append('*');
                                }

                                sb.Append(partName);
                                if (part.IsOptional)
                                {
                                    sb.Append('?');
                                }

                                sb.Append('}');
                            }
                        }
                    }
                }
            }

            var simplifiedRoute = StringBuilderCache.GetStringAndRelease(sb);

            return string.IsNullOrEmpty(simplifiedRoute) ? "/" : simplifiedRoute.ToLowerInvariant();
        }

        /// <summary>
        /// Holds state that we want to pass between diagnostic source events
        /// </summary>
        internal class RequestTrackingFeature
        {
            public RequestTrackingFeature(IPathString originalPath)
            {
                OriginalPath = originalPath;
            }

            /// <summary>
            /// Gets or sets a value indicating whether the pipeline using endpoint routing
            /// </summary>
            public bool IsUsingEndpointRouting { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether this is the first pipeline execution
            /// </summary>
            public bool IsFirstPipelineExecution { get; set; } = true;

            /// <summary>
            /// Gets or sets a value indicating the route as calculated by endpoint routing (if available)
            /// </summary>
            public string Route { get; set; }

            /// <summary>
            /// Gets or sets a value indicating the resource name as calculated by the endpoint routing(if available)
            /// </summary>
            public string ResourceName { get; set; }

            /// <summary>
            /// Gets a value indicating the original combined Path and PathBase
            /// </summary>
            public IPathString OriginalPath { get; }

            public bool MatchesOriginalPath(IHttpRequest request)
            {
                if (!request.PathBase.HasValue)
                {
                    return OriginalPath.Equals(request.Path.Instance, StringComparison.OrdinalIgnoreCase);
                }

                return OriginalPath.StartsWithSegments(
                           request.PathBase.Instance,
                           StringComparison.OrdinalIgnoreCase,
                           out var remaining)
                    && remaining.DuckCast<IPathString>().Equals(request.Path.Instance, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
#endif
