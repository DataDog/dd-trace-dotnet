// <copyright file="AspNetCoreDiagnosticObserver2.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK
using System;
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

namespace Datadog.Trace.DiagnosticListeners;

/// <summary>
/// Instruments ASP.NET Core.
/// <para/>
/// Unfortunately, ASP.NET Core only uses one <see cref="System.Diagnostics.DiagnosticListener"/> instance
/// for everything so we also only create one observer to ensure best performance.
/// <para/>
/// Hosting events: https://github.com/dotnet/aspnetcore/blob/master/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs
/// </summary>
internal sealed class AspNetCoreDiagnosticObserver2 : DiagnosticObserver
{
    public const IntegrationId IntegrationId = Configuration.IntegrationId.AspNetCore;

    private const string DiagnosticListenerName = "Microsoft.AspNetCore";
    private const string HttpRequestInOperationName = "aspnet_core.request";

    private static readonly int PrefixLength = "Microsoft.AspNetCore.".Length;

    private static readonly Type? EndpointFeatureType =
        Assembly.GetAssembly(typeof(RouteValueDictionary))
               ?.GetType("Microsoft.AspNetCore.Http.Features.IEndpointFeature", throwOnError: false);

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AspNetCoreDiagnosticObserver2>();
    private static readonly AspNetCoreHttpRequestHandler AspNetCoreRequestHandler = new AspNetCoreHttpRequestHandler(Log, HttpRequestInOperationName, IntegrationId);
    private readonly Tracer? _tracer;
    private readonly Security? _security;
    private string? _hostingHttpRequestInStartEventKey;
    private string? _mvcBeforeActionEventKey;
    private string? _mvcAfterActionEventKey;
    private string? _hostingUnhandledExceptionEventKey;
    private string? _diagnosticsUnhandledExceptionEventKey;
    private string? _hostingHttpRequestInStopEventKey;
    private string? _routingEndpointMatchedKey;

    public AspNetCoreDiagnosticObserver2()
        : this(null, null)
    {
    }

    public AspNetCoreDiagnosticObserver2(Tracer? tracer, Security? security)
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
                // Don't provide a resource name here, as we will likely replace it as part of the request
                // If we don't, update it in OnHostingHttpRequestInStop or OnHostingUnhandledException
                var scope = AspNetCoreRequestHandler.StartAspNetCorePipelineScope2(tracer, CurrentSecurity, httpContext);
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

        var span = tracer.InternalActiveScope?.Span;

        if (span != null)
        {
            var tags = span.Tags as AspNetCoreTags2;
            if (tags is null || !arg.TryDuckCast<AspNetCoreDiagnosticObserver.HttpRequestInEndpointMatchedStruct>(out var typedArg))
            {
                // Shouldn't happen in normal execution
                return;
            }

            HttpContext httpContext = typedArg.HttpContext;

            // NOTE: This event is when the routing middleware selects an endpoint. Additional middleware (e.g
            //       Authorization/CORS) may still run, and the endpoint itself has not started executing.

            if (EndpointFeatureType is null || httpContext.Features[EndpointFeatureType] is not { } rawEndpointFeature)
            {
                return;
            }

            object? rawEndpoint = null;
            if (rawEndpointFeature.TryDuckCast<EndpointFeatureProxy>(out var endpointFeatureInterface))
            {
                // .NET Core 3+
                rawEndpoint = endpointFeatureInterface.GetEndpoint();
            }
            else if (rawEndpointFeature.TryDuckCast<AspNetCoreDiagnosticObserver.EndpointFeatureStruct>(out var endpointFeatureStruct))
            {
                // < .NET Core 3
                rawEndpoint = endpointFeatureStruct.Endpoint;
            }

            if (rawEndpoint is null || !rawEndpoint.TryDuckCast<RouteEndpoint>(out var routeEndpoint))
            {
                // Unable to cast to either type, shouldn't happen!
                return;
            }

            var routePattern = routeEndpoint.RoutePattern;
            var normalizedRoute = routePattern.RawText?.ToLowerInvariant();

            if (tags.AspNetCoreEndpoint is null)
            {
                // first execution, so update everything
                tags.AspNetCoreEndpoint = routeEndpoint.DisplayName;
                tags.HttpRoute = tags.AspNetCoreRoute = normalizedRoute;

                var request = httpContext.Request.DuckCast<AspNetCoreDiagnosticObserver.HttpRequestStruct>();
                RouteValueDictionary routeValues = request.RouteValues;
                // No need to ToLowerInvariant() these strings, as we lower case
                // the whole route later in SimplifyRoutePattern
                object raw;
                var controllerName = routeValues.TryGetValue("controller", out raw)
                                            ? (raw as string)?.ToLowerInvariant()
                                            : null;
                var actionName = routeValues.TryGetValue("action", out raw)
                                        ? (raw as string)?.ToLowerInvariant()
                                        : null;
                var areaName = routeValues.TryGetValue("area", out raw)
                                      ? (raw as string)?.ToLowerInvariant()
                                      : null;
                var pagePath = routeValues.TryGetValue("page", out raw)
                                      ? (raw as string)?.ToLowerInvariant()
                                      : null;

                var resourcePathName = AspNetCoreResourceNameHelper.SimplifyRoutePattern(
                    routePattern,
                    routeValues,
                    areaName: areaName,
                    controllerName: controllerName,
                    actionName: actionName,
                    tracer.Settings.ExpandRouteTemplatesEnabled);

                span.ResourceName = $"{tags.HttpMethod} {request.PathBase.ToUriComponent()}{resourcePathName}";

                tags.AspNetCoreController = controllerName;
                tags.AspNetCoreAction = actionName;
                tags.AspNetCoreArea = areaName;
                tags.AspNetCorePage = pagePath;

                var security = CurrentSecurity;
                if (security.Enabled)
                {
                    security.CheckPathParams(httpContext, span, routeValues);
                }

                if (Iast.Iast.Instance.Settings.Enabled)
                {
                    span.Context?.TraceContext?.IastRequestContext?.AddRequestData(httpContext.Request, routeValues);
                }
            }
            else if (normalizedRoute is not null)
            {
                // this is not the first execution, record the route
                tags.SubsequentRoutes ??= new();
                tags.SubsequentRoutes.Add(normalizedRoute);
            }
        }
    }

    private void OnMvcBeforeAction(object arg)
    {
        var tracer = CurrentTracer;
        var security = CurrentSecurity;

        // Only need to trace if we _aren't_ doing endpoint routing
        var shouldTrace = tracer.Settings.IsIntegrationEnabled(IntegrationId)
                       && tracer.InternalActiveScope?.Span is { Tags: AspNetCoreTags2 { AspNetCoreEndpoint: null } };

        var shouldSecure = security.Enabled;
        var shouldUseIast = Iast.Iast.Instance.Settings.Enabled;

        if (!shouldTrace && !shouldSecure && !shouldUseIast)
        {
            return;
        }

        var span = tracer.InternalActiveScope?.Span;

        if (span != null && arg.TryDuckCast<AspNetCoreDiagnosticObserver.BeforeActionStruct>(out var typedArg))
        {
            HttpContext httpContext = typedArg.HttpContext;
            HttpRequest request = httpContext.Request;

            // NOTE: This event is the start of the action pipeline. The action has been selected, the route
            //       has been selected but no filters have run and model binding hasn't occurred.
            if (shouldTrace)
            {
                // This is only called with new route names, so parent tags are always AspNetCoreTags2
                // an is only executed if we're _not_ using endpoint routing

                var tags = (AspNetCoreTags2)span.Tags;

                // var isFirstExecution = tags.SubsequentRoutes is null;
                // if (isFirstExecution)
                // {
                //
                //     if (!trackingFeature.MatchesOriginalPath(httpContext.Request))
                //     {
                //         // URL has changed from original, so treat this execution as a "subsequent" request
                //         // Typically occurs for 404s for example
                //         isFirstExecution = false;
                //     }
                // }
                // in a < .NET Core 3.1 app here (no endpoint routing)
                ActionDescriptor actionDescriptor = typedArg.ActionDescriptor;
                IDictionary<string, string> routeValues = actionDescriptor.RouteValues;

                string? raw;
                var controllerName = routeValues.TryGetValue("controller", out raw)
                                            ? raw.ToLowerInvariant()
                                            : null;
                var actionName = routeValues.TryGetValue("action", out raw)
                                        ? raw.ToLowerInvariant()
                                        : null;
                var areaName = routeValues.TryGetValue("area", out raw)
                                      ? raw.ToLowerInvariant()
                                      : null;
                var pagePath = routeValues.TryGetValue("page", out raw)
                                      ? raw.ToLowerInvariant()
                                      : null;

                string? rawRouteTemplate = actionDescriptor.AttributeRouteInfo?.Template;
                RouteTemplate? routeTemplate = null;
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

                    span.ResourceName = $"{tags.HttpMethod} {request.PathBase.ToUriComponent()}{resourcePathName}";
                    tags.HttpRoute = tags.AspNetCoreRoute = routeTemplate.TemplateText?.ToLowerInvariant();
                }
                else
                {
                    // fallback
                    if (!string.IsNullOrEmpty(span.ResourceName))
                    {
                        span.ResourceName = AspNetCoreRequestHandler.GetDefaultResourceName(httpContext.Request);
                    }

                    if (rawRouteTemplate is not null)
                    {
                        tags.HttpRoute = tags.AspNetCoreRoute = rawRouteTemplate.ToLowerInvariant();
                    }
                }

                tags.AspNetCoreAction = actionName;
                tags.AspNetCoreController = controllerName;
                tags.AspNetCoreArea = areaName;
                tags.AspNetCorePage = pagePath;
            }

            if (shouldSecure)
            {
                CurrentSecurity.CheckPathParamsFromAction(httpContext, span, typedArg.ActionDescriptor?.Parameters, typedArg.RouteData.Values);
            }

            if (shouldUseIast)
            {
                span.Context?.TraceContext?.IastRequestContext?.AddRequestData(request, typedArg.RouteData?.Values);
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

        // TODO: Is this correct? Does it collect _all_ the baggage? Might we be missing some if it's set after this?
        if (scope is not null && ReferenceEquals(scope.Span.OperationName, HttpRequestInOperationName))
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
        }
    }

    private void OnHostingHttpRequestInStop(object arg)
    {
        var tracer = CurrentTracer;

        if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
        {
            return;
        }

        var scope = tracer.InternalActiveScope;
        var httpContext = scope is not null ? arg.DuckCast<AspNetCoreDiagnosticObserver.HttpRequestInStopStruct>().HttpContext : null;

        AspNetCoreRequestHandler.StopAspNetCorePipelineScope(tracer, CurrentSecurity, scope, httpContext);
    }

    private void OnHostingUnhandledException(object arg)
    {
        var tracer = CurrentTracer;

        if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
        {
            return;
        }

        var span = tracer.InternalActiveScope?.Span;

        if (span != null && arg.TryDuckCast<AspNetCoreDiagnosticObserver.UnhandledExceptionStruct>(out var unhandledStruct))
        {
            AspNetCoreRequestHandler.HandleAspNetCoreException(tracer, CurrentSecurity, span, unhandledStruct.HttpContext, unhandledStruct.Exception);
        }
    }
}
#endif
