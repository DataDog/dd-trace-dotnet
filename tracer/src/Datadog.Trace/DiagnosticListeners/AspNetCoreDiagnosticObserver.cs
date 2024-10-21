// <copyright file="AspNetCoreDiagnosticObserver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Iast;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Http;
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
        public const IntegrationId IntegrationId = Configuration.IntegrationId.AspNetCore;

        private const string DiagnosticListenerName = "Microsoft.AspNetCore";
        private const string HttpRequestInOperationName = "aspnet_core.request";
        private const string MvcOperationName = "aspnet_core_mvc.request";

        private static readonly int PrefixLength = "Microsoft.AspNetCore.".Length;

        private static readonly Type EndpointFeatureType =
            Assembly.GetAssembly(typeof(RouteValueDictionary))
                   ?.GetType("Microsoft.AspNetCore.Http.Features.IEndpointFeature", throwOnError: false);

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AspNetCoreDiagnosticObserver>();
        private static readonly AspNetCoreHttpRequestHandler AspNetCoreRequestHandler = new AspNetCoreHttpRequestHandler(Log, HttpRequestInOperationName, IntegrationId);
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

        private Security CurrentSecurity => _security ?? Security.Instance;

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

        private static string GetLegacyResourceName(BeforeActionStruct typedArg)
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

            return $"{httpMethod} {routeTemplate}";
        }

        private static Span StartMvcCoreSpan(
            Tracer tracer,
            AspNetCoreHttpRequestHandler.RequestTrackingFeature trackingFeature,
            BeforeActionStruct typedArg,
            HttpContext httpContext,
            HttpRequest request)
        {
            // Create a child span for the MVC action
            var mvcSpanTags = new AspNetCoreMvcTags();
            var mvcScope = tracer.StartActiveInternal(MvcOperationName, tags: mvcSpanTags);
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            var span = mvcScope.Span;
            span.Type = SpanTypes.Web;

            // StartMvcCoreSpan is only called with new route names, so parent tags are always AspNetCoreEndpointTags
            var rootSpan = trackingFeature.RootScope.Span;
            var rootSpanTags = (AspNetCoreEndpointTags)rootSpan.Tags;

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
                    var resourcePathName = AspNetCoreResourceNameHelper.SimplifyRouteTemplate(
                        routeTemplate,
                        typedArg.RouteData.Values,
                        areaName: areaName,
                        controllerName: controllerName,
                        actionName: actionName,
                        expandRouteParameters: tracer.Settings.ExpandRouteTemplatesEnabled);

                    resourceName = $"{rootSpanTags.HttpMethod} {request.PathBase.ToUriComponent()}{resourcePathName}";

                    aspNetRoute = routeTemplate?.TemplateText.ToLowerInvariant();
                }
            }

            // mirror the parent if we couldn't extract a route for some reason
            // (and the parent is not using the placeholder resource name)
            span.ResourceName = resourceName
                             ?? (string.IsNullOrEmpty(rootSpan.ResourceName)
                                     ? AspNetCoreRequestHandler.GetDefaultResourceName(httpContext.Request)
                                     : rootSpan.ResourceName);

            mvcSpanTags.AspNetCoreAction = actionName;
            mvcSpanTags.AspNetCoreController = controllerName;
            mvcSpanTags.AspNetCoreArea = areaName;
            mvcSpanTags.AspNetCorePage = pagePath;
            mvcSpanTags.AspNetCoreRoute = aspNetRoute;

            if (!isUsingEndpointRouting && isFirstExecution)
            {
                // If we're using endpoint routing or this is a pipeline re-execution,
                // these will already be set correctly
                rootSpanTags.AspNetCoreRoute = aspNetRoute;
                rootSpan.ResourceName = span.ResourceName;
                rootSpanTags.HttpRoute = aspNetRoute;
            }

            return span;
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
                var httpContext = requestStruct.HttpContext;
                if (shouldTrace)
                {
                    // Use an empty resource name here, as we will likely replace it as part of the request
                    // If we don't, update it in OnHostingHttpRequestInStop or OnHostingUnhandledException
                    var scope = AspNetCoreRequestHandler.StartAspNetCorePipelineScope(tracer, CurrentSecurity, httpContext, resourceName: string.Empty);
                    if (shouldSecure)
                    {
                        CoreHttpContextStore.Instance.Set(httpContext);
                        SecurityCoordinator.ReportWafInitInfoOnce(security, scope.Span);
                    }
                }
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

            if (arg.TryDuckCast<HttpRequestInEndpointMatchedStruct>(out var typedArg)
             && typedArg.HttpContext is { } httpContext
             && httpContext.Features.Get<AspNetCoreHttpRequestHandler.RequestTrackingFeature>() is { RootScope.Span: { } rootSpan } trackingFeature)
            {
                if (rootSpan.Tags is not AspNetCoreEndpointTags tags)
                {
                    // customer is using legacy resource names
                    return;
                }

                var isFirstExecution = trackingFeature.IsFirstPipelineExecution;
                if (isFirstExecution)
                {
                    trackingFeature.IsUsingEndpointRouting = true;
                    trackingFeature.IsFirstPipelineExecution = false;

                    if (!trackingFeature.MatchesOriginalPath(httpContext.Request))
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

                RouteEndpoint? routeEndpoint = null;

                if (rawEndpointFeature.TryDuckCast<EndpointFeatureProxy>(out var endpointFeatureInterface))
                {
                    if (endpointFeatureInterface.GetEndpoint().TryDuckCast<RouteEndpoint>(out var routeEndpointObj))
                    {
                        routeEndpoint = routeEndpointObj;
                    }
                }

                if (routeEndpoint is null && rawEndpointFeature.TryDuckCast<EndpointFeatureStruct>(out var endpointFeatureStruct))
                {
                    if (endpointFeatureStruct.Endpoint.TryDuckCast<RouteEndpoint>(out var routeEndpointObj))
                    {
                        routeEndpoint = routeEndpointObj;
                    }
                }

                if (routeEndpoint is null)
                {
                    // Unable to cast to either type
                    return;
                }

                if (isFirstExecution)
                {
                    tags.AspNetCoreEndpoint = routeEndpoint.Value.DisplayName;
                }

                var routePattern = routeEndpoint.Value.RoutePattern;

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

                var resourcePathName = AspNetCoreResourceNameHelper.SimplifyRoutePattern(
                    routePattern,
                    routeValues,
                    areaName: areaName,
                    controllerName: controllerName,
                    actionName: actionName,
                    tracer.Settings.ExpandRouteTemplatesEnabled);

                var resourceName = $"{tags.HttpMethod} {request.PathBase.ToUriComponent()}{resourcePathName}";

                // NOTE: We could set the controller/action/area tags on the parent span
                // But instead we re-extract them in the MVC endpoint as these are MVC
                // constructs. this is likely marginally less efficient, but simplifies the
                // already complex logic in the MVC handler
                // Overwrite the route in the parent span
                trackingFeature.ResourceName = resourceName;
                if (isFirstExecution)
                {
                    rootSpan.ResourceName = resourceName;
                    tags.AspNetCoreRoute = normalizedRoute;
                    tags.HttpRoute = normalizedRoute;
                }

                CurrentSecurity.CheckPathParams(httpContext, rootSpan, routeValues);

                if (Iast.Iast.Instance.Settings.Enabled)
                {
                    rootSpan.Context?.TraceContext?.IastRequestContext?.AddRequestData(httpContext.Request, routeValues);
                }
            }
        }

        private void OnMvcBeforeAction(object arg)
        {
            var tracer = CurrentTracer;
            var security = CurrentSecurity;
            var shouldTrace = tracer.Settings.IsIntegrationEnabled(IntegrationId);
            var shouldSecure = security.Enabled;
            var shouldUseIast = Iast.Iast.Instance.Settings.Enabled;

            if (!shouldTrace && !shouldSecure && !shouldUseIast)
            {
                return;
            }

            if (arg.TryDuckCast<BeforeActionStruct>(out var typedArg)
             && typedArg.HttpContext is { } httpContext
             && httpContext.Features.Get<AspNetCoreHttpRequestHandler.RequestTrackingFeature>() is { RootScope.Span: { } rootSpan } trackingFeature)
            {
                HttpRequest request = httpContext.Request;

                // NOTE: This event is the start of the action pipeline. The action has been selected, the route
                //       has been selected but no filters have run and model binding hasn't occurred.
                Span span = null;
                if (shouldTrace)
                {
                    if (!tracer.Settings.RouteTemplateResourceNamesEnabled)
                    {
                        // override the parent's resource name with the simplified MVC route template
                        rootSpan.ResourceName = GetLegacyResourceName(typedArg);
                    }
                    else
                    {
                        span = StartMvcCoreSpan(tracer, trackingFeature, typedArg, httpContext, request);
                    }
                }

                if (span is not null)
                {
                    CurrentSecurity.CheckPathParamsFromAction(httpContext, span, typedArg.ActionDescriptor?.Parameters, typedArg.RouteData.Values);
                }

                if (shouldUseIast)
                {
                    rootSpan.Context?.TraceContext?.IastRequestContext?.AddRequestData(request, typedArg.RouteData?.Values);
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

            var scope = tracer.InternalActiveScope;

            if (scope is not null && ReferenceEquals(scope.Span.OperationName, MvcOperationName))
            {
                try
                {
                    // Extract data from the Activity
                    var activity = Activity.ActivityListener.GetCurrentActivity();
                    if (activity is not null)
                    {
                        foreach (var activityTag in activity.Tags)
                        {
                            scope.Span.SetTag(activityTag.Key, activityTag.Value);
                        }

                        foreach (var activityBag in activity.Baggage)
                        {
                            scope.Span.SetTag(activityBag.Key, activityBag.Value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error extracting activity data.");
                }

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

            if (arg.DuckCast<HttpRequestInStopStruct>().HttpContext is { } httpContext
             && httpContext.Features.Get<AspNetCoreHttpRequestHandler.RequestTrackingFeature>() is { RootScope: { } rootScope })
            {
                AspNetCoreRequestHandler.StopAspNetCorePipelineScope(tracer, CurrentSecurity, rootScope, httpContext);
            }

            CoreHttpContextStore.Instance.Remove();
            // If we don't have a scope, no need to call Stop pipeline
        }

        private void OnHostingUnhandledException(object arg)
        {
            var tracer = CurrentTracer;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                return;
            }

            if (arg.TryDuckCast<UnhandledExceptionStruct>(out var unhandledStruct)
             && unhandledStruct.HttpContext is { } httpContext
             && httpContext.Features.Get<AspNetCoreHttpRequestHandler.RequestTrackingFeature>() is { RootScope.Span: { } rootSpan })
            {
                AspNetCoreRequestHandler.HandleAspNetCoreException(tracer, CurrentSecurity, rootSpan, httpContext, unhandledStruct.Exception);
            }

            // If we don't have a span, no need to call Handle exception
        }

        [DuckCopy]
        internal struct HttpRequestInStartStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public HttpContext HttpContext;
        }

        [DuckCopy]
        internal struct HttpRequestInStopStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public HttpContext HttpContext;
        }

        [DuckCopy]
        internal struct UnhandledExceptionStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public HttpContext HttpContext;

            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public Exception Exception;
        }

        [DuckCopy]
        internal struct BeforeActionStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public HttpContext HttpContext;

            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public ActionDescriptor ActionDescriptor;

            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public RouteData RouteData;
        }

        [DuckCopy]
        internal struct BadHttpRequestExceptionStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase | BindingFlags.NonPublic)]
            public int StatusCode;
        }

        [DuckCopy]
        internal struct HttpRequestInEndpointMatchedStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public HttpContext HttpContext;
        }

        /// <summary>
        /// Proxy for ducktyping IEndpointFeature when the interface is not implemented explicitly
        /// </summary>
        /// <seealso cref="EndpointFeatureProxy"/>
        [DuckCopy]
        internal struct EndpointFeatureStruct
        {
            public object Endpoint;
        }

        [DuckCopy]
        internal struct HttpRequestStruct
        {
            public string Method;
            public RouteValueDictionary RouteValues;
            public PathString PathBase;
        }

        /// <summary>
        /// Proxy for https://github1s.com/dotnet/aspnetcore/blob/v3.0.3/src/Http/Routing/src/Patterns/RoutePatternPathSegment.cs
        /// </summary>
        [DuckCopy]
        internal struct RoutePatternPathSegmentStruct
        {
            public IEnumerable Parts;
        }

        /// <summary>
        /// Proxy for https://github1s.com/dotnet/aspnetcore/blob/v3.0.3/src/Http/Routing/src/Patterns/RoutePatternLiteralPart.cs
        /// and https://github1s.com/dotnet/aspnetcore/blob/v3.0.3/src/Http/Routing/src/Patterns/RoutePatternSeparatorPart.cs
        /// </summary>
        [DuckCopy]
        internal struct RoutePatternContentPartStruct
        {
            public string Content;
        }

        /// <summary>
        /// Proxy for https://github1s.com/dotnet/aspnetcore/blob/v3.0.3/src/Http/Routing/src/Patterns/RoutePatternParameterPart.cs
        /// </summary>
        [DuckCopy]
        internal struct RoutePatternParameterPartStruct
        {
            public string Name;
            public bool IsOptional;
            public bool IsCatchAll;
            public bool EncodeSlashes;
        }
    }
}
#endif
