// <copyright file="SingleSpanAspNetCoreDiagnosticObserver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NET6_0_OR_GREATER
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
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.SpanCodeOrigin;
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
    internal sealed class SingleSpanAspNetCoreDiagnosticObserver : DiagnosticObserver
    {
        public const IntegrationId IntegrationId = Configuration.IntegrationId.AspNetCore;

        private const string DiagnosticListenerName = "Microsoft.AspNetCore";
        private const string HttpRequestInOperationName = "aspnet_core.request";
        private const string MvcOperationName = "aspnet_core_mvc.request";

        private static readonly int PrefixLength = "Microsoft.AspNetCore.".Length;

        private static readonly Type? EndpointFeatureType =
            Assembly.GetAssembly(typeof(RouteValueDictionary))
                   ?.GetType("Microsoft.AspNetCore.Http.Features.IEndpointFeature", throwOnError: false);

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SingleSpanAspNetCoreDiagnosticObserver>();
        private static readonly AspNetCoreHttpRequestHandler AspNetCoreRequestHandler = new AspNetCoreHttpRequestHandler(Log, HttpRequestInOperationName, IntegrationId);
        private readonly Tracer? _tracer;
        private readonly Security? _security;
        private readonly Iast.Iast? _iast;
        private readonly SpanCodeOrigin? _spanCodeOrigin;
        private string? _hostingHttpRequestInStartEventKey;
        private string? _mvcBeforeActionEventKey;
        private string? _mvcAfterActionEventKey;
        private string? _hostingUnhandledExceptionEventKey;
        private string? _diagnosticsUnhandledExceptionEventKey;
        private string? _hostingHttpRequestInStopEventKey;
        private string? _routingEndpointMatchedKey;

        public SingleSpanAspNetCoreDiagnosticObserver()
            : this(null, null, null, null)
        {
        }

        public SingleSpanAspNetCoreDiagnosticObserver(Tracer? tracer, Security? security, Iast.Iast? iast, SpanCodeOrigin? spanCodeOrigin)
        {
            _tracer = tracer;
            _security = security;
            _iast = iast;
            _spanCodeOrigin = spanCodeOrigin;
        }

        protected override string ListenerName => DiagnosticListenerName;

        private Tracer CurrentTracer => _tracer ?? Tracer.Instance;

        private Security CurrentSecurity => _security ?? Security.Instance;

        private Iast.Iast CurrentIast => _iast ?? Iast.Iast.Instance;

        private SpanCodeOrigin? CurrentCodeOrigin => _spanCodeOrigin ?? DebuggerManager.Instance.CodeOrigin;

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

        private void OnHostingHttpRequestInStart(object arg)
        {
            var tracer = CurrentTracer;
            var security = CurrentSecurity;
            var shouldTrace = tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId);
            var shouldSecure = security.AppsecEnabled;

            if (!shouldTrace && !shouldSecure)
            {
                return;
            }

            if (arg.TryDuckCast<AspNetCoreDiagnosticObserver.HttpRequestInStartStruct>(out var requestStruct))
            {
                var httpContext = requestStruct.HttpContext;
                if (shouldTrace)
                {
                    // Use an empty resource name here, as we will likely replace it as part of the request
                    // If we don't, update it in OnHostingHttpRequestInStop or OnHostingUnhandledException
                    // If the app is using resource-based sampling rules, then we need to set a resource straight
                    // away, so force that by using null.
                    var resourceName = tracer.CurrentTraceSettings.HasResourceBasedSamplingRule ? null : string.Empty;
                    var scope = AspNetCoreRequestHandler.StartAspNetCorePipelineScope(tracer, CurrentSecurity, httpContext, resourceName, new AspNetCoreSingleSpanTags());
                    if (shouldSecure)
                    {
                        CoreHttpContextStore.Instance.Set(httpContext);
                        var securityReporter = new SecurityReporter(scope.Span, new SecurityCoordinator.HttpTransport(httpContext));
                        securityReporter.ReportWafInitInfoOnce(security.WafInitResult);
                    }
                }
            }
        }

        private void OnRoutingEndpointMatched(object arg)
        {
            var tracer = CurrentTracer;

            if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
            {
                return;
            }

            if (arg.TryDuckCast<AspNetCoreDiagnosticObserver.HttpRequestInEndpointMatchedStruct>(out var typedArg)
             && typedArg.HttpContext is { } httpContext
             && httpContext.Features.Get<AspNetCoreHttpRequestHandler.SingleSpanRequestTrackingFeature>() is { RootScope.Span: { Tags: AspNetCoreSingleSpanTags tags } rootSpan } trackingFeature)
            {
                var isFirstExecution = trackingFeature.IsFirstPipelineExecution;
                if (isFirstExecution)
                {
                    // Update this for the next call
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
                else if (rawEndpointFeature.TryDuckCast<AspNetCoreDiagnosticObserver.EndpointFeatureStruct>(out var endpointFeatureStruct))
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

                if (CurrentCodeOrigin is { Settings.CodeOriginForSpansEnabled: true })
                {
                    var method = routeEndpoint?.RequestDelegate?.Method;
                    if (method != null)
                    {
                        CurrentCodeOrigin?.SetCodeOriginForEntrySpan(rootSpan, routeEndpoint?.RequestDelegate?.Target?.GetType() ?? method.DeclaringType, method);
                    }
                    else if (routeEndpoint?.RequestDelegate?.TryDuckCast<Target>(out var target) == true && target is { Handler: { } handler })
                    {
                        Log.Debug("RouteEndpoint?.RequestDelegate?.Method is null. Extracting code origin from RouteEndpoint.RequestDelegate.Target.Handler {Handler}", handler);
                        CurrentCodeOrigin?.SetCodeOriginForEntrySpan(rootSpan, handler.Target?.GetType(), handler.Method);
                    }
                    else
                    {
                        Log.Debug("RouteEndpoint?.RequestDelegate?.Method is null and could not extract handler from RouteEndpoint.RequestDelegate.Target");
                    }
                }

                if (isFirstExecution)
                {
                    tags.AspNetCoreEndpoint = routeEndpoint!.Value.DisplayName;
                }

                var routePattern = routeEndpoint!.Value.RoutePattern.DuckCast<RoutePattern>();

                var request = httpContext.Request.DuckCast<AspNetCoreDiagnosticObserver.HttpRequestStruct>();
                RouteValueDictionary routeValues = request.RouteValues;
                // No need to ToLowerInvariant() these strings, as we lower case
                // the whole route later
                object? raw;
                var controllerName = routeValues.TryGetValue("controller", out raw)
                                            ? raw as string
                                            : null;
                var actionName = routeValues.TryGetValue("action", out raw)
                                     ? raw as string
                                     : null;
                var areaName = routeValues.TryGetValue("area", out raw)
                                   ? raw as string
                                   : null;

                var resourcePathName = AspNetCoreResourceNameHelper.SimplifyRoutePattern(
                    routePattern,
                    routeValues,
                    areaName: areaName,
                    controllerName: controllerName,
                    actionName: actionName,
                    tracer.Settings.ExpandRouteTemplatesEnabled);

                // NOTE: We could set the controller/action/area tags on the parent span
                // But instead we re-extract them in the MVC endpoint as these are MVC
                // constructs. This is likely marginally less efficient, but simplifies the
                // already complex logic in the MVC handler
                // Overwrite/Update the route in the parent span
                if (isFirstExecution)
                {
                    // TODO optimize this allocation?
                    rootSpan.ResourceName = $"{tags.HttpMethod} {request.PathBase.ToUriComponent()}{resourcePathName}";
                    tags.AspNetCoreRoute = routePattern.RawText?.ToLowerInvariant();
                }

                // We check appsec enabled in here, but this avoids the method call if it's not needed
                var security = CurrentSecurity;
                if (security.AppsecEnabled)
                {
                    security.CheckPathParamsAndSessionId(httpContext, rootSpan, routeValues);
                }

                if (CurrentIast.Settings.Enabled)
                {
                    rootSpan.Context?.TraceContext?.IastRequestContext?.AddRequestData(httpContext.Request, routeValues);
                }
            }
        }

        private void OnMvcBeforeAction(object arg)
        {
            var security = CurrentSecurity;
            var shouldSecure = security.AppsecEnabled;
            var shouldUseIast = CurrentIast.Settings.Enabled;
            var isCodeOriginEnabled = CurrentCodeOrigin is { Settings.CodeOriginForSpansEnabled: true };

            if (!shouldSecure && !shouldUseIast && !isCodeOriginEnabled)
            {
                return;
            }

            if (arg.TryDuckCast<AspNetCoreDiagnosticObserver.BeforeActionStruct>(out var typedArg)
             && typedArg.HttpContext is { } httpContext
             && httpContext.Features.Get<AspNetCoreHttpRequestHandler.SingleSpanRequestTrackingFeature>() is { RootScope.Span: { } rootSpan } trackingFeature)
            {
                if (isCodeOriginEnabled)
                {
                    if (AspNetCoreDiagnosticObserver.TryGetTypeAndMethod(typedArg, out var type, out var method))
                    {
                        CurrentCodeOrigin!.SetCodeOriginForEntrySpan(rootSpan, type, method);
                    }
                    else
                    {
                        Log.Debug("Could not extract type and method from {ActionDescriptor}", typedArg.ActionDescriptor?.DisplayName);
                    }
                }

                CurrentSecurity.CheckPathParamsFromAction(httpContext, rootSpan, typedArg.ActionDescriptor?.Parameters, typedArg.RouteData.Values);

                if (shouldUseIast)
                {
                    rootSpan.Context.TraceContext.IastRequestContext?.AddRequestData(httpContext.Request, typedArg.RouteData?.Values);
                }
            }
        }

        private void OnMvcAfterAction(object arg)
        {
            var tracer = CurrentTracer;

            if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
            {
                return;
            }

            var scope = tracer.InternalActiveScope;

            if (scope is { Span: { } span }
             && ReferenceEquals(span.OperationName, HttpRequestInOperationName)
                // To avoid the expensive reading of activity tags etc if they don't have "otel compatibility enabled"
             && tracer.Settings.IsActivityListenerEnabled)
            {
                AddActivityTags(span);
            }

            static void AddActivityTags(Span span)
            {
                try
                {
                    // Extract data from the Activity
                    var activity = Activity.ActivityListener.GetCurrentActivity();
#pragma warning disable DDDUCK001 // Checking IDuckType for null
                    if (activity is not null)
                    {
                        foreach (var activityTag in activity.Tags)
                        {
                            span.SetTag(activityTag.Key, activityTag.Value);
                        }

                        foreach (var activityBag in activity.Baggage)
                        {
                            span.SetTag(activityBag.Key, activityBag.Value);
                        }
                    }
#pragma warning restore DDDUCK001 // Checking IDuckType for null
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error extracting activity data.");
                }
            }
        }

        private void OnHostingHttpRequestInStop(object arg)
        {
            var tracer = CurrentTracer;

            if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
            {
                return;
            }

            if (arg.DuckCast<AspNetCoreDiagnosticObserver.HttpRequestInStopStruct>().HttpContext is { } httpContext
             && httpContext.Features.Get<AspNetCoreHttpRequestHandler.SingleSpanRequestTrackingFeature>() is { RootScope: { } rootScope, ProxyScope: var proxyScope })
            {
                AspNetCoreRequestHandler.StopAspNetCorePipelineScope(tracer, CurrentSecurity, rootScope, httpContext, proxyScope);
            }

            CoreHttpContextStore.Instance.Remove();
            // If we don't have a scope, no need to call Stop pipeline
        }

        private void OnHostingUnhandledException(object arg)
        {
            var tracer = CurrentTracer;

            if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
            {
                return;
            }

            if (arg.TryDuckCast<AspNetCoreDiagnosticObserver.UnhandledExceptionStruct>(out var unhandledStruct)
             && unhandledStruct.HttpContext is { } httpContext
             && httpContext.Features.Get<AspNetCoreHttpRequestHandler.SingleSpanRequestTrackingFeature>() is { RootScope.Span: { } rootSpan, ProxyScope: var proxyScope })
            {
                AspNetCoreRequestHandler.HandleAspNetCoreException(tracer, CurrentSecurity, rootSpan, httpContext, unhandledStruct.Exception, proxyScope);
            }

            // If we don't have a span, no need to call Handle exception
        }
    }
}
#endif
