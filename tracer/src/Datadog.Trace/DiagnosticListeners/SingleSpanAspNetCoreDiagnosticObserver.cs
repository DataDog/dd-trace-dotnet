// <copyright file="SingleSpanAspNetCoreDiagnosticObserver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Tagging;
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
    internal sealed class SingleSpanAspNetCoreDiagnosticObserver : DiagnosticObserver
    {
        public const IntegrationId IntegrationId = Configuration.IntegrationId.AspNetCore;

        private const string DiagnosticListenerName = "Microsoft.AspNetCore";
        private const string HttpRequestInOperationName = "aspnet_core.request";
        private const string MvcOperationName = "aspnet_core_mvc.request";

#if NETCOREAPP
        private static readonly int PrefixLength = "Microsoft.AspNetCore.".Length;
#endif
        private static readonly Type EndpointFeatureType =
            Assembly.GetAssembly(typeof(RouteValueDictionary))
                   ?.GetType("Microsoft.AspNetCore.Http.Features.IEndpointFeature", throwOnError: false);

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SingleSpanAspNetCoreDiagnosticObserver>();
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

        public SingleSpanAspNetCoreDiagnosticObserver()
            : this(null, null)
        {
        }

        public SingleSpanAspNetCoreDiagnosticObserver(Tracer tracer, Security security)
        {
            _tracer = tracer;
            _security = security;
        }

        protected override string ListenerName => DiagnosticListenerName;

        private Tracer CurrentTracer => _tracer ?? Tracer.Instance;

        private Security CurrentSecurity => _security ?? Security.Instance;

        protected override void OnNext(string eventName, object arg)
        {
#if NETCOREAPP
            var lastChar = eventName[^1];
#else
            var lastChar = eventName[eventName.Length - 1];
#endif

            if (lastChar == 't')
            {
                if (ReferenceEquals(eventName, _hostingHttpRequestInStartEventKey))
                {
                    OnHostingHttpRequestInStart(arg);
                }
#if NETCOREAPP
                else if (eventName.AsSpan().Slice(PrefixLength).SequenceEqual("Hosting.HttpRequestIn.Start"))
#else
                else if (eventName == "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start")
#endif
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
#if NETCOREAPP
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

#else
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
#endif

                return;
            }

            if (lastChar == 'p')
            {
                if (ReferenceEquals(eventName, _hostingHttpRequestInStopEventKey))
                {
                    OnHostingHttpRequestInStop(arg);
                }
#if NETCOREAPP
                else if (eventName.AsSpan().Slice(PrefixLength).SequenceEqual("Hosting.HttpRequestIn.Stop"))
#else
                else if (eventName == "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop")
#endif
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
#if NETCOREAPP
                else if (eventName.AsSpan().Slice(PrefixLength).SequenceEqual("Routing.EndpointMatched"))
#else
                else if (eventName == "Microsoft.AspNetCore.Routing.EndpointMatched")
#endif
                {
                    _routingEndpointMatchedKey = eventName;
                    OnRoutingEndpointMatched(arg);
                }

                return;
            }
        }

        private static void UpdateSpanWithMvc(
            Tracer tracer,
            SingleSpanTrackingFeature trackingFeature,
            AspNetCoreDiagnosticObserver.BeforeActionStruct typedArg,
            HttpContext httpContext,
            HttpRequest request)
        {
            var rootSpan = trackingFeature.RootScope.Span;
            var rootSpanTags = (AspNetCoreSingleSpanTags)rootSpan.Tags;

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

            // We only need to extract these if we're _not_ doing endpoint routing
            // OR this is a re-execution, otherwise they're already extracted in OnRoutingEndpointMatched
            if (!trackingFeature.IsUsingEndpointRouting || !isFirstExecution)
            {
                ActionDescriptor actionDescriptor = typedArg.ActionDescriptor;
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

                var aspNetRoute = routeTemplate?.TemplateText.ToLowerInvariant();

                if (!trackingFeature.IsUsingEndpointRouting && isFirstExecution)
                {
                    // not using endpoint routing, so need to update everything
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

                    // record the values
                    rootSpanTags.AspNetCoreAction = actionName;
                    rootSpanTags.AspNetCoreController = controllerName;
                    rootSpanTags.AspNetCoreArea = areaName;
                    rootSpanTags.AspNetCorePage = pagePath;

                    string resourceName = null;
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
                    }

                    // not using endpoint routing, so need to update everything
                    rootSpan.ResourceName = resourceName
                                         ?? (string.IsNullOrEmpty(rootSpan.ResourceName)
                                                 ? AspNetCoreRequestHandler.GetDefaultResourceName(httpContext.Request)
                                                 : rootSpan.ResourceName);
                    rootSpanTags.AspNetCoreRoute = aspNetRoute;
                    rootSpanTags.HttpRoute = aspNetRoute;
                }

                if (!isFirstExecution)
                {
                    // TODO: record the aspnetroute in a list of "re-executed" routes
                    // So we don't lose any data compared to previous versions
                }
            }
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

            if (arg.TryDuckCast<AspNetCoreDiagnosticObserver.HttpRequestInStartStruct>(out var requestStruct))
            {
                HttpContext httpContext = requestStruct.HttpContext;
                if (shouldTrace)
                {
                    // Use an empty resource name here, as we will likely replace it as part of the request
                    // If we don't, update it in OnHostingHttpRequestInStop or OnHostingUnhandledException
                    var scope = AspNetCoreRequestHandler.StartAspNetCorePipelineScope(
                        tracer,
                        CurrentSecurity,
                        httpContext,
                        resourceName: string.Empty,
                        new AspNetCoreSingleSpanTags(),
                        static (path, scope) => new SingleSpanTrackingFeature(path, scope));
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
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                return;
            }

            if (arg.TryDuckCast<AspNetCoreDiagnosticObserver.HttpRequestInEndpointMatchedStruct>(out var typedArg)
             && typedArg.HttpContext is { } httpContext
             && httpContext.Features.Get<SingleSpanTrackingFeature>() is { RootScope.Span: { } rootSpan } trackingFeature)
            {
                if (rootSpan.Tags is not AspNetCoreSingleSpanTags tags)
                {
                    // should never happen
                    return;
                }

                var isFirstExecution = trackingFeature.IsFirstPipelineExecution;
                if (isFirstExecution)
                {
                    trackingFeature.IsUsingEndpointRouting = true;
                    trackingFeature.IsFirstPipelineExecution = false;

                    // TODO: Do we need this? Can we determine this from the values we store when we create the tags initially?
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

                if (routeEndpoint is null && rawEndpointFeature.TryDuckCast<AspNetCoreDiagnosticObserver.EndpointFeatureStruct>(out var endpointFeatureStruct))
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

                var request = httpContext.Request.DuckCast<AspNetCoreDiagnosticObserver.HttpRequestStruct>();
                RouteValueDictionary routeValues = request.RouteValues;
                object raw;
                string controllerName = routeValues.TryGetValue("controller", out raw)
                                            ? (raw as string)?.ToLowerInvariant()
                                            : null;
                string actionName = routeValues.TryGetValue("action", out raw)
                                        ? (raw as string)?.ToLowerInvariant()
                                        : null;
                string areaName = routeValues.TryGetValue("area", out raw)
                                      ? (raw as string)?.ToLowerInvariant()
                                      : null;

                string pagePath = routeValues.TryGetValue("page", out raw)
                                      ? (raw as string)?.ToLowerInvariant()
                                       : null;

                var resourcePathName = AspNetCoreResourceNameHelper.SimplifyRoutePattern(
                    routePattern,
                    routeValues,
                    areaName: areaName,
                    controllerName: controllerName,
                    actionName: actionName,
                    tracer.Settings.ExpandRouteTemplatesEnabled);

                var resourceName = $"{tags.HttpMethod} {request.PathBase.ToUriComponent()}{resourcePathName}";

                if (isFirstExecution)
                {
                    rootSpan.ResourceName = resourceName;
                    tags.AspNetCoreRoute = normalizedRoute;
                    tags.HttpRoute = normalizedRoute;

                    // We're not going to create a child span, so set these on the span directly
                    tags.AspNetCoreAction = actionName;
                    tags.AspNetCoreController = controllerName;
                    tags.AspNetCoreArea = areaName;
                    tags.AspNetCorePage = pagePath;
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

            if (arg.TryDuckCast<AspNetCoreDiagnosticObserver.BeforeActionStruct>(out var typedArg)
             && typedArg.HttpContext is { } httpContext
             && httpContext.Features.Get<SingleSpanTrackingFeature>() is { RootScope.Span: { } rootSpan } trackingFeature)
            {
                HttpRequest request = httpContext.Request;

                // NOTE: This event is the start of the action pipeline. The action has been selected, the route
                //       has been selected but no filters have run and model binding hasn't occurred.
                if (shouldTrace)
                {
                    UpdateSpanWithMvc(tracer, trackingFeature, typedArg, httpContext, request);
                }

                if (rootSpan is not null)
                {
                    CurrentSecurity.CheckPathParamsFromAction(httpContext, rootSpan, typedArg.ActionDescriptor?.Parameters, typedArg.RouteData.Values);
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

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
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

            if (arg.DuckCast<AspNetCoreDiagnosticObserver.HttpRequestInStopStruct>().HttpContext is { } httpContext
             && httpContext.Features.Get<SingleSpanTrackingFeature>() is { RootScope: { } rootScope })
            {
                AspNetCoreRequestHandler.StopAspNetCorePipelineScope(tracer, CurrentSecurity, rootScope, httpContext);
            }

            // If we don't have a scope, no need to call Stop pipeline
        }

        private void OnHostingUnhandledException(object arg)
        {
            var tracer = CurrentTracer;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                return;
            }

            if (arg.TryDuckCast<AspNetCoreDiagnosticObserver.UnhandledExceptionStruct>(out var unhandledStruct)
             && unhandledStruct.HttpContext is { } httpContext
             && httpContext.Features.Get<SingleSpanTrackingFeature>() is { RootScope.Span: { } rootSpan })
            {
                AspNetCoreRequestHandler.HandleAspNetCoreException(tracer, CurrentSecurity, rootSpan, httpContext, unhandledStruct.Exception);
            }

            // If we don't have a span, no need to call Handle exception
        }

        /// <summary>
        /// Holds state that we want to pass between diagnostic source events
        /// </summary>
        internal class SingleSpanTrackingFeature
        {
            public SingleSpanTrackingFeature(PathString originalPath, Scope rootAspNetCoreScope)
            {
                OriginalPath = originalPath;
                RootScope = rootAspNetCoreScope;
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
            /// Gets a value indicating the original combined Path and PathBase
            /// </summary>
            public PathString OriginalPath { get; }

            /// <summary>
            /// Gets the root ASP.NET Core Scope
            /// </summary>
            public Scope RootScope { get; }

            public bool MatchesOriginalPath(HttpRequest request)
            {
                if (!request.PathBase.HasValue)
                {
                    return OriginalPath.Equals(request.Path, StringComparison.OrdinalIgnoreCase);
                }

                return OriginalPath.StartsWithSegments(
                           request.PathBase,
                           StringComparison.OrdinalIgnoreCase,
                           out var remaining)
                    && remaining.Equals(request.Path, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
#endif
