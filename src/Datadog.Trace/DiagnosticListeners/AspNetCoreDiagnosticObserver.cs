// <copyright file="AspNetCoreDiagnosticObserver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Transport.Http;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;

namespace Datadog.Trace.DiagnosticListeners
{
    /// <summary>
    /// Instruments ASP.NET Core.
    /// <para/>
    /// Unfortunately, ASP.NET Core only uses one <see cref="System.Diagnostics.DiagnosticListener"/> instance
    /// for everything so we also only create one observer to ensure best performance.
    /// <para/>
    /// Hosting events: https://github.com/dotnet/aspnetcore/blob/master/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs
    /// </summary>
    internal sealed class AspNetCoreDiagnosticObserver : DiagnosticObserver
    {
        public static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.AspNetCore));

        private const string DiagnosticListenerName = "Microsoft.AspNetCore";
        private const string HttpRequestInOperationName = "aspnet_core.request";
        private const string MvcOperationName = "aspnet_core_mvc.request";
        private const string NoHostSpecified = "UNKNOWN_HOST";

        private static readonly int PrefixLength = "Microsoft.AspNetCore.".Length;

        private static readonly Type EndpointFeatureType =
            Assembly.GetAssembly(typeof(RouteValueDictionary))
                   ?.GetType("Microsoft.AspNetCore.Http.Features.IEndpointFeature", throwOnError: false);

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AspNetCoreDiagnosticObserver>();
        private readonly Tracer _tracer;
        private readonly Security _security;

        private string _hostingHttpRequestInStartEventKey;
        private string _mvcBeforeActionEventKey;
        private string _mvcAfterActionEventKey;
        private string _hostingUnhandledExceptionEventKey;
        private string _diagnosticsUnhandledExceptionEventKey;
        private string _hostingHttpRequestInStopEventKey;
        private string _routingEndpointMatchedKey;

        public AspNetCoreDiagnosticObserver()
            : this(null, null)
        {
        }

        public AspNetCoreDiagnosticObserver(Tracer tracer, Security security)
        {
            _tracer = tracer;
            _security = security;
        }

        protected override string ListenerName => DiagnosticListenerName;

        private Tracer CurrentTracer => _tracer ?? Tracer.Instance;

        private IDatadogSecurity CurrentSecurity => _security ?? Security.Instance;

#if NETCOREAPP
        protected override void OnNext(string eventName, object arg)
        {
            var lastChar = eventName[^1];

            if (lastChar == 't')
            {
                if (ReferenceEquals(eventName, _hostingHttpRequestInStartEventKey))
                {
                    OnHostingHttpRequestInStart(arg);
                }
                else if (eventName.AsSpan().Slice(PrefixLength).SequenceEqual("Hosting.HttpRequestIn.Start"))
                {
                    _hostingHttpRequestInStartEventKey = eventName;
                    OnHostingHttpRequestInStart(arg);
                }

                return;
            }

            if (lastChar == 'n')
            {
                if (ReferenceEquals(eventName, _mvcBeforeActionEventKey))
                {
                    OnMvcBeforeAction(arg);
                    return;
                }
                else if (ReferenceEquals(eventName, _mvcAfterActionEventKey))
                {
                    OnMvcAfterAction(arg);
                    return;
                }
                else if (ReferenceEquals(eventName, _hostingUnhandledExceptionEventKey) ||
                    ReferenceEquals(eventName, _diagnosticsUnhandledExceptionEventKey))
                {
                    OnHostingUnhandledException(arg);
                    return;
                }

                var suffix = eventName.AsSpan().Slice(PrefixLength);

                if (suffix.SequenceEqual("Mvc.BeforeAction"))
                {
                    _mvcBeforeActionEventKey = eventName;
                    OnMvcBeforeAction(arg);
                }
                else if (suffix.SequenceEqual("Mvc.AfterAction"))
                {
                    _mvcAfterActionEventKey = eventName;
                    OnMvcAfterAction(arg);
                }
                else if (suffix.SequenceEqual("Hosting.UnhandledException"))
                {
                    _hostingUnhandledExceptionEventKey = eventName;
                    OnHostingUnhandledException(arg);
                }
                else if (suffix.SequenceEqual("Diagnostics.UnhandledException"))
                {
                    _diagnosticsUnhandledExceptionEventKey = eventName;
                    OnHostingUnhandledException(arg);
                }

                return;
            }

            if (lastChar == 'p')
            {
                if (ReferenceEquals(eventName, _hostingHttpRequestInStopEventKey))
                {
                    OnHostingHttpRequestInStop(arg);
                }
                else if (eventName.AsSpan().Slice(PrefixLength).SequenceEqual("Hosting.HttpRequestIn.Stop"))
                {
                    _hostingHttpRequestInStopEventKey = eventName;
                    OnHostingHttpRequestInStop(arg);
                }

                return;
            }

            if (lastChar == 'd')
            {
                if (ReferenceEquals(eventName, _routingEndpointMatchedKey))
                {
                    OnRoutingEndpointMatched(arg);
                }
                else if (eventName.AsSpan().Slice(PrefixLength).SequenceEqual("Routing.EndpointMatched"))
                {
                    _routingEndpointMatchedKey = eventName;
                    OnRoutingEndpointMatched(arg);
                }

                return;
            }
        }

#else
        protected override void OnNext(string eventName, object arg)
        {
            var lastChar = eventName[eventName.Length - 1];

            if (lastChar == 't')
            {
                if (ReferenceEquals(eventName, _hostingHttpRequestInStartEventKey))
                {
                    OnHostingHttpRequestInStart(arg);
                }
                else if (eventName == "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start")
                {
                    _hostingHttpRequestInStartEventKey = eventName;
                    OnHostingHttpRequestInStart(arg);
                }

                return;
            }

            if (lastChar == 'n')
            {
                if (ReferenceEquals(eventName, _mvcBeforeActionEventKey))
                {
                    OnMvcBeforeAction(arg);
                    return;
                }
                else if (ReferenceEquals(eventName, _mvcAfterActionEventKey))
                {
                    OnMvcAfterAction(arg);
                    return;
                }
                else if (ReferenceEquals(eventName, _hostingUnhandledExceptionEventKey) ||
                    ReferenceEquals(eventName, _diagnosticsUnhandledExceptionEventKey))
                {
                    OnHostingUnhandledException(arg);
                    return;
                }

                switch (eventName)
                {
                    case "Microsoft.AspNetCore.Mvc.BeforeAction":
                        _mvcBeforeActionEventKey = eventName;
                        OnMvcBeforeAction(arg);
                        break;

                    case "Microsoft.AspNetCore.Mvc.AfterAction":
                        _mvcAfterActionEventKey = eventName;
                        OnMvcAfterAction(arg);
                        break;

                    case "Microsoft.AspNetCore.Hosting.UnhandledException":
                        _hostingUnhandledExceptionEventKey = eventName;
                        OnHostingUnhandledException(arg);
                        break;
                    case "Microsoft.AspNetCore.Diagnostics.UnhandledException":
                        _diagnosticsUnhandledExceptionEventKey = eventName;
                        OnHostingUnhandledException(arg);
                        break;
                }

                return;
            }

            if (lastChar == 'p')
            {
                if (ReferenceEquals(eventName, _hostingHttpRequestInStopEventKey))
                {
                    OnHostingHttpRequestInStop(arg);
                }
                else if (eventName == "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop")
                {
                    _hostingHttpRequestInStopEventKey = eventName;
                    OnHostingHttpRequestInStop(arg);
                }

                return;
            }

            if (lastChar == 'd')
            {
                if (ReferenceEquals(eventName, _routingEndpointMatchedKey))
                {
                    OnRoutingEndpointMatched(arg);
                }
                else if (eventName == "Microsoft.AspNetCore.Routing.EndpointMatched")
                {
                    _routingEndpointMatchedKey = eventName;
                    OnRoutingEndpointMatched(arg);
                }

                return;
            }
        }
#endif

        private static SpanContext ExtractPropagatedContext(HttpRequest request)
        {
            try
            {
                // extract propagation details from http headers
                var requestHeaders = request.Headers;

                if (requestHeaders != null)
                {
                    return SpanContextPropagator.Instance.Extract(new HeadersCollectionAdapter(requestHeaders));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting propagated HTTP headers.");
            }

            return null;
        }

        private static IEnumerable<KeyValuePair<string, string>> ExtractHeaderTags(HttpRequest request, IDatadogTracer tracer)
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
                        return SpanContextPropagator.Instance.ExtractHeaderTags(new HeadersCollectionAdapter(requestHeaders), settings.HeaderTags, defaultTagPrefix: SpanContextPropagator.HttpRequestHeadersTagPrefix);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error extracting propagated HTTP headers.");
                }
            }

            return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        private static string SimplifyRoutePattern(
            RoutePattern routePattern,
            RouteValueDictionary routeValueDictionary,
            string areaName,
            string controllerName,
            string actionName)
        {
            var maxSize = routePattern.RawText.Length
                        + (string.IsNullOrEmpty(areaName) ? 0 : Math.Max(areaName.Length - 4, 0)) // "area".Length
                        + (string.IsNullOrEmpty(controllerName) ? 0 : Math.Max(controllerName.Length - 10, 0)) // "controller".Length
                        + (string.IsNullOrEmpty(actionName) ? 0 : Math.Max(actionName.Length - 6, 0)) // "action".Length
                        + 1; // '/' prefix

            var sb = StringBuilderCache.Acquire(maxSize);

            foreach (var pathSegment in routePattern.PathSegments)
            {
                foreach (var part in pathSegment.DuckCast<RoutePatternPathSegmentStruct>().Parts)
                {
                    if (DuckType.CanCreate<RoutePatternContentPartStruct>(part))
                    {
                        var contentPart = part.DuckCast<RoutePatternContentPartStruct>();
                        sb.Append('/');
                        sb.Append(contentPart.Content);
                    }
                    else if (DuckType.CanCreate<RoutePatternParameterPartStruct>(part))
                    {
                        var parameter = part.DuckCast<RoutePatternParameterPartStruct>();

                        var parameterName = parameter.Name;
                        if (parameterName.Equals("area", StringComparison.OrdinalIgnoreCase))
                        {
                            sb.Append('/');
                            sb.Append(areaName);
                        }
                        else if (parameterName.Equals("controller", StringComparison.OrdinalIgnoreCase))
                        {
                            sb.Append('/');
                            sb.Append(controllerName);
                        }
                        else if (parameterName.Equals("action", StringComparison.OrdinalIgnoreCase))
                        {
                            sb.Append('/');
                            sb.Append(actionName);
                        }
                        else if (!parameter.IsOptional || routeValueDictionary.ContainsKey(parameterName))
                        {
                            sb.Append("/{");
                            if (parameter.IsCatchAll)
                            {
                                if (parameter.EncodeSlashes)
                                {
                                    sb.Append("**");
                                }
                                else
                                {
                                    sb.Append('*');
                                }
                            }

                            sb.Append(parameterName);
                            if (parameter.IsOptional)
                            {
                                sb.Append('?');
                            }

                            sb.Append('}');
                        }
                    }
                }
            }

            var simplifiedRoute = StringBuilderCache.GetStringAndRelease(sb);

            return string.IsNullOrEmpty(simplifiedRoute) ? "/" : simplifiedRoute.ToLowerInvariant();
        }

        private static string SimplifyRoutePattern(
            RouteTemplate routePattern,
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

            foreach (var pathSegment in routePattern.Segments)
            {
                foreach (var part in pathSegment.Parts)
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

            var simplifiedRoute = StringBuilderCache.GetStringAndRelease(sb);

            return string.IsNullOrEmpty(simplifiedRoute) ? "/" : simplifiedRoute.ToLowerInvariant();
        }

        private static void SetLegacyResourceNames(BeforeActionStruct typedArg, Span span)
        {
            ActionDescriptor actionDescriptor = typedArg.ActionDescriptor;
            HttpRequest request = typedArg.HttpContext.Request;

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

        private static Span StartMvcCoreSpan(Tracer tracer, Span parentSpan, BeforeActionStruct typedArg, HttpContext httpContext, HttpRequest request)
        {
            // Create a child span for the MVC action
            var mvcSpanTags = new AspNetCoreEndpointTags();
            var mvcScope = tracer.StartActiveWithTags(MvcOperationName, parentSpan.Context, tags: mvcSpanTags);
            var span = mvcScope.Span;
            span.Type = SpanTypes.Web;

            var trackingFeature = httpContext.Features.Get<RequestTrackingFeature>();
            var isUsingEndpointRouting = trackingFeature.IsUsingEndpointRouting;

            var isFirstExecution = trackingFeature.IsFirstPipelineExecution;
            if (isFirstExecution)
            {
                trackingFeature.IsFirstPipelineExecution = false;
                var url = httpContext.Request.GetUrl();
                if (!string.Equals(url, trackingFeature.OriginalUrl))
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

                    resourceName = $"{trackingFeature.HttpMethod} {request.PathBase}{resourcePathName}";
                    aspNetRoute = routeTemplate?.TemplateText.ToLowerInvariant();
                }
            }

            // mirror the parent if we couldn't extract a route
            span.ResourceName = resourceName ?? parentSpan.ResourceName;

            mvcSpanTags.AspNetCoreAction = actionName;
            mvcSpanTags.AspNetCoreController = controllerName;
            mvcSpanTags.AspNetCoreArea = areaName;
            mvcSpanTags.AspNetCorePage = pagePath;
            mvcSpanTags.AspNetCoreRoute = aspNetRoute;

            if (!isUsingEndpointRouting && isFirstExecution)
            {
                // If we're using endpoint routing or this is a pipeline re-execution,
                // these will already be set correctly
                if (parentSpan.Tags is AspNetCoreEndpointTags parentTags)
                {
                    parentTags.AspNetCoreRoute = aspNetRoute;
                }

                parentSpan.ResourceName = span.ResourceName;
            }

            return span;
        }

        private Span StartCoreSpan(Tracer tracer, HttpContext httpContext, HttpRequest request)
        {
            string host = request.Host.Value;
            string httpMethod = request.Method?.ToUpperInvariant() ?? "UNKNOWN";
            string url = request.GetUrl();

            if (tracer.Settings.RouteTemplateResourceNamesEnabled)
            {
                httpContext.Features.Set(new RequestTrackingFeature
                {
                    HttpMethod = httpMethod,
                    OriginalUrl = url,
                });
            }

            string absolutePath = request.Path.Value;

            if (request.PathBase.HasValue)
            {
                absolutePath = request.PathBase.Value + absolutePath;
            }

            string resourceUrl = UriHelpers.GetCleanUriPath(absolutePath)
                                           .ToLowerInvariant();

            string resourceName = $"{httpMethod} {resourceUrl}";

            SpanContext propagatedContext = ExtractPropagatedContext(request);
            var tagsFromHeaders = ExtractHeaderTags(request, tracer);

            var tags = tracer.Settings.RouteTemplateResourceNamesEnabled ? new AspNetCoreEndpointTags() : new AspNetCoreTags();
            var scope = tracer.StartActiveWithTags(HttpRequestInOperationName, propagatedContext, tags: tags);

            scope.Span.DecorateWebServerSpan(resourceName, httpMethod, host, url, tags, tagsFromHeaders);

            tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: true);
            Log.Warning($"Created span {scope.Span.SpanId} with {resourceName}, {scope.Span.ResourceName}");
            return scope.Span;
        }

        private void OnHostingHttpRequestInStart(object arg)
        {
            var tracer = CurrentTracer;
            var security = CurrentSecurity;

            var shouldTrace = tracer.Settings.IsIntegrationEnabled(IntegrationId);
            var shouldSecure = security.Enabled;

            if (!shouldTrace && !shouldSecure)
            {
                return;
            }

            if (arg.TryDuckCast<HttpRequestInStartStruct>(out var requestStruct))
            {
                HttpContext httpContext = requestStruct.HttpContext;
                HttpRequest request = httpContext.Request;
                Span span = null;
                if (shouldTrace)
                {
                    span = StartCoreSpan(tracer, httpContext, request);
                }

                if (shouldSecure)
                {
                    RaiseIntrumentationEvent(security, httpContext, request, span);
                }
            }
        }

        private void RaiseIntrumentationEvent(IDatadogSecurity security, HttpContext context, HttpRequest request, Span span, RouteData routeData = null)
        {
            try
            {
                var dic = request.PrepareArgsForWaf(routeData);
                security.InstrumentationGateway.RaiseEvent(dic, new HttpTransport(context), span);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred raising instrumentation event");
            }
        }

        private void OnRoutingEndpointMatched(object arg)
        {
            var tracer = CurrentTracer;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId) ||
                !tracer.Settings.RouteTemplateResourceNamesEnabled)
            {
                return;
            }

            Span span = tracer.ActiveScope?.Span;

            if (span != null)
            {
                var tags = span.Tags as AspNetCoreEndpointTags;
                if (tags is null || !arg.TryDuckCast<HttpRequestInEndpointMatchedStruct>(out var typedArg))
                {
                    // Shouldn't happen in normal execution
                    return;
                }

                HttpContext httpContext = typedArg.HttpContext;
                var trackingFeature = httpContext.Features.Get<RequestTrackingFeature>();
                var isFirstExecution = trackingFeature.IsFirstPipelineExecution;
                if (isFirstExecution)
                {
                    trackingFeature.IsUsingEndpointRouting = true;
                    trackingFeature.IsFirstPipelineExecution = false;

                    var url = httpContext.Request.GetUrl();
                    if (!string.Equals(url, trackingFeature.OriginalUrl))
                    {
                        // URL has changed from original, so treat this execution as a "subsequent" request
                        // Typically occurs for 404s for example
                        isFirstExecution = false;
                    }
                }

                // NOTE: This event is when the routing middleware selects an endpoint. Additional middleware (e.g
                //       Authorization/CORS) may still run, and the endpoint itself has not started executing.

                if (EndpointFeatureType is null)
                {
                    return;
                }

                var rawEndpointFeature = httpContext.Features[EndpointFeatureType];
                if (rawEndpointFeature is null)
                {
                    return;
                }

                RouteEndpoint? endpoint = null;

                if (rawEndpointFeature.TryDuckCast<IEndpointFeature>(out var endpointFeatureInterface))
                {
                    endpoint = endpointFeatureInterface.GetEndpoint();
                }

                if (endpoint is null && rawEndpointFeature.TryDuckCast<EndpointFeatureStruct>(out var endpointFeatureStruct))
                {
                    endpoint = endpointFeatureStruct.Endpoint;
                }

                if (endpoint is null)
                {
                    // Unable to cast to either type
                    return;
                }

                if (isFirstExecution)
                {
                    tags.AspNetCoreEndpoint = endpoint.Value.DisplayName;
                }

                var routePattern = endpoint.Value.RoutePattern;

                // Have to pass this value through to the MVC span, as not available there
                var normalizedRoute = routePattern.RawText?.ToLowerInvariant();
                trackingFeature.Route = normalizedRoute;

                var request = httpContext.Request.DuckCast<HttpRequestStruct>();
                RouteValueDictionary routeValues = request.RouteValues;

                // No need to ToLowerInvariant() these strings, as we lower case
                // the whole route later
                object raw;
                string controllerName = routeValues.TryGetValue("controller", out raw)
                                        ? raw as string
                                        : null;
                string actionName = routeValues.TryGetValue("action", out raw)
                                        ? raw as string
                                        : null;
                string areaName = routeValues.TryGetValue("area", out raw)
                                      ? raw as string
                                      : null;

                var resourcePathName = SimplifyRoutePattern(
                    routePattern,
                    routeValues,
                    areaName: areaName,
                    controllerName: controllerName,
                    actionName: actionName);

                var resourceName = $"{trackingFeature.HttpMethod} {request.PathBase}{resourcePathName}";

                // NOTE: We could set the controller/action/area tags on the parent span
                // But instead we re-extract them in the MVC endpoint as these are MVC
                // constructs. this is likely marginally less efficient, but simplifies the
                // already complex logic in the MVC handler
                // Overwrite the route in the parent span
                trackingFeature.ResourceName = resourceName;
                if (isFirstExecution)
                {
                    span.ResourceName = resourceName;
                    tags.AspNetCoreRoute = normalizedRoute;
                }
            }
        }

        private void OnMvcBeforeAction(object arg)
        {
            var tracer = CurrentTracer;
            var security = CurrentSecurity;

            var shouldTrace = tracer.Settings.IsIntegrationEnabled(IntegrationId);
            var shouldSecure = security.Enabled;

            if (!shouldTrace && !shouldSecure)
            {
                return;
            }

            Span parentSpan = tracer.ActiveScope?.Span;

            if (parentSpan != null && arg.TryDuckCast<BeforeActionStruct>(out var typedArg))
            {
                HttpContext httpContext = typedArg.HttpContext;
                HttpRequest request = httpContext.Request;

                // NOTE: This event is the start of the action pipeline. The action has been selected, the route
                //       has been selected but no filters have run and model binding hasn't occurred.
                Span span = null;
                if (shouldTrace)
                {
                    if (!tracer.Settings.RouteTemplateResourceNamesEnabled)
                    {
                        SetLegacyResourceNames(typedArg, parentSpan);
                    }
                    else
                    {
                        span = StartMvcCoreSpan(tracer, parentSpan, typedArg, httpContext, request);
                    }
                }

                if (shouldSecure)
                {
                    RaiseIntrumentationEvent(security, httpContext, request, span ?? parentSpan, typedArg.RouteData);
                }
            }
        }

        private void OnMvcAfterAction(object arg)
        {
            var tracer = CurrentTracer;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId) ||
                !tracer.Settings.RouteTemplateResourceNamesEnabled)
            {
                return;
            }

            var scope = tracer.ActiveScope;

            if (scope is not null && ReferenceEquals(scope.Span.OperationName, MvcOperationName))
            {
                scope.Dispose();
            }
        }

        private void OnHostingHttpRequestInStop(object arg)
        {
            var tracer = CurrentTracer;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                return;
            }

            var scope = tracer.ActiveScope;

            if (scope != null)
            {
                // if we had an unhandled exception, the status code is already updated
                if (!scope.Span.Error && arg.TryDuckCast<HttpRequestInStopStruct>(out var httpRequest))
                {
                    HttpContext httpContext = httpRequest.HttpContext;
                    scope.Span.SetHttpStatusCode(httpContext.Response.StatusCode, isServer: true);
                    scope.Span.SetHeaderTags(new HeadersCollectionAdapter(httpContext.Response.Headers), tracer.Settings.HeaderTags, defaultTagPrefix: SpanContextPropagator.HttpResponseHeadersTagPrefix);
                }

                scope.Dispose();
            }
        }

        private void OnHostingUnhandledException(object arg)
        {
            var tracer = CurrentTracer;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                return;
            }

            var span = tracer.ActiveScope?.Span;

            if (span != null && arg.TryDuckCast<UnhandledExceptionStruct>(out var unhandledStruct))
            {
                span.SetException(unhandledStruct.Exception);
                int statusCode = 500;

                if (unhandledStruct.Exception.TryDuckCast<BadHttpRequestExceptionStruct>(out var badRequestException))
                {
                    statusCode = badRequestException.StatusCode;
                }

                // Generic unhandled exceptions are converted to 500 errors by Kestrel
                span.SetHttpStatusCode(statusCode: statusCode, isServer: true);
            }
        }

        [DuckCopy]
        public struct HttpRequestInStartStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public HttpContext HttpContext;
        }

        [DuckCopy]
        public struct HttpRequestInStopStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public HttpContext HttpContext;
        }

        [DuckCopy]
        public struct UnhandledExceptionStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public Exception Exception;
        }

        [DuckCopy]
        public struct BeforeActionStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public HttpContext HttpContext;

            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public ActionDescriptor ActionDescriptor;

            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public RouteData RouteData;
        }

        [DuckCopy]
        public struct BadHttpRequestExceptionStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase | BindingFlags.NonPublic)]
            public int StatusCode;
        }

        [DuckCopy]
        public struct HttpRequestInEndpointMatchedStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public HttpContext HttpContext;
        }

        /// <summary>
        /// Proxy for ducktyping IEndpointFeature when the interface is not implemented explictly
        /// </summary>
        /// <seealso cref="IEndpointFeature"/>
        [DuckCopy]
        public struct EndpointFeatureStruct
        {
            public RouteEndpoint Endpoint;
        }

        [DuckCopy]
        public struct HttpRequestStruct
        {
            public string Method;
            public RouteValueDictionary RouteValues;
            public PathString PathBase;
        }

        /// <summary>
        /// Proxy for https://github1s.com/dotnet/aspnetcore/blob/v3.0.3/src/Http/Routing/src/Patterns/RoutePatternPathSegment.cs
        /// </summary>
        [DuckCopy]
        public struct RoutePatternPathSegmentStruct
        {
            public IEnumerable Parts;
        }

        /// <summary>
        /// Proxy for https://github1s.com/dotnet/aspnetcore/blob/v3.0.3/src/Http/Routing/src/Patterns/RoutePatternLiteralPart.cs
        /// and https://github1s.com/dotnet/aspnetcore/blob/v3.0.3/src/Http/Routing/src/Patterns/RoutePatternSeparatorPart.cs
        /// </summary>
        [DuckCopy]
        public struct RoutePatternContentPartStruct
        {
            public string Content;
        }

        /// <summary>
        /// Proxy for https://github1s.com/dotnet/aspnetcore/blob/v3.0.3/src/Http/Routing/src/Patterns/RoutePatternParameterPart.cs
        /// </summary>
        [DuckCopy]
        public struct RoutePatternParameterPartStruct
        {
            public string Name;
            public bool IsOptional;
            public bool IsCatchAll;
            public bool EncodeSlashes;
        }

        private readonly struct HeadersCollectionAdapter : IHeadersCollection
        {
            private readonly IHeaderDictionary _headers;

            public HeadersCollectionAdapter(IHeaderDictionary headers)
            {
                _headers = headers;
            }

            public IEnumerable<string> GetValues(string name)
            {
                if (_headers.TryGetValue(name, out var values))
                {
                    return values.ToArray();
                }

                return Enumerable.Empty<string>();
            }

            public void Set(string name, string value)
            {
                throw new NotImplementedException();
            }

            public void Add(string name, string value)
            {
                throw new NotImplementedException();
            }

            public void Remove(string name)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Holds state that we want to pass between diagnostic source events
        /// </summary>
        private class RequestTrackingFeature
        {
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
            /// Gets or sets the HTTP method, as it requires normalization, so avoids repeatedly calculations
            /// </summary>
            public string HttpMethod { get; set; }

            /// <summary>
            /// Gets or Sets the original URL received by the pipeline
            /// </summary>
            public string OriginalUrl { get; set; }
        }
    }
}
#endif
