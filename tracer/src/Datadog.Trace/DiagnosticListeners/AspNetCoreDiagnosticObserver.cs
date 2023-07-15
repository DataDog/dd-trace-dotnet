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
using System.Runtime.CompilerServices;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
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
        private static readonly AspNetCoreHttpRequestHandler AspNetCoreRequestHandler = new(Log, HttpRequestInOperationName, IntegrationId);
        private Tracer _tracer;
        private Security _security;
        private bool? _integrationEnabled;
        private int _eventNameCacheFlags = 0; // 1 | 2 | 4 | 8 | 16 | 32 | 64 = (7 Flags) =  127 all enabled
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

        private Tracer CurrentTracer => _tracer ?? (_tracer = Tracer.Instance);

        private Security CurrentSecurity => _security ?? (_security = Security.Instance);

        private bool IsIntegrationEnabled
        {
            get
            {
                _integrationEnabled ??= CurrentTracer.Settings.IsIntegrationEnabled(IntegrationId);
                return _integrationEnabled.Value;
            }
        }

        protected override void OnNext(string eventName, object arg)
        {
            if (ReferenceEquals(eventName, _hostingHttpRequestInStartEventKey))
            {
                OnHostingHttpRequestInStart(arg);
                return;
            }

            if (ReferenceEquals(eventName, _mvcBeforeActionEventKey))
            {
                OnMvcBeforeAction(arg);
                return;
            }

            if (ReferenceEquals(eventName, _mvcAfterActionEventKey))
            {
                OnMvcAfterAction(arg);
                return;
            }

            if (ReferenceEquals(eventName, _hostingHttpRequestInStopEventKey))
            {
                OnHostingHttpRequestInStop(arg);
                return;
            }

            if (ReferenceEquals(eventName, _routingEndpointMatchedKey))
            {
                OnRoutingEndpointMatched(arg);
                return;
            }

            if (ReferenceEquals(eventName, _hostingUnhandledExceptionEventKey) || ReferenceEquals(eventName, _diagnosticsUnhandledExceptionEventKey))
            {
                OnHostingUnhandledException(arg);
                return;
            }

            OnNextSlow(eventName, arg);
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
            var mvcSpanTags = new AspNetCoreMvcTags();
            var mvcScope = tracer.StartActiveInternal(MvcOperationName, parentSpan.Context, tags: mvcSpanTags);
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            var span = mvcScope.Span;
            span.Type = SpanTypes.Web;

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

            string controllerName = null;
            string actionName = null;
            string areaName = null;
            string pagePath = null;

            // if RouteValues is a dictionary we can cast and use the internal Enumerator with no allocations.
            if (routeValues is Dictionary<string, string> dctRouteValues)
            {
                // We can also avoid calculating the key hash all the time and just enumerate and bailout when we have all the data.
                var count = 0;
                foreach (var routeValue in dctRouteValues)
                {
                    if (count == 4)
                    {
                        break;
                    }

                    if (string.Equals(routeValue.Key, "controller", StringComparison.OrdinalIgnoreCase))
                    {
                        controllerName = routeValue.Value?.ToLowerInvariant();
                        count++;
                        continue;
                    }

                    if (string.Equals(routeValue.Key, "action", StringComparison.OrdinalIgnoreCase))
                    {
                        actionName = routeValue.Value?.ToLowerInvariant();
                        count++;
                        continue;
                    }

                    if (string.Equals(routeValue.Key, "area", StringComparison.OrdinalIgnoreCase))
                    {
                        areaName = routeValue.Value?.ToLowerInvariant();
                        count++;
                        continue;
                    }

                    if (string.Equals(routeValue.Key, "page", StringComparison.OrdinalIgnoreCase))
                    {
                        pagePath = routeValue.Value?.ToLowerInvariant();
                        count++;
                        continue;
                    }
                }
            }
            else
            {
                GetControllerData(routeValues, out controllerName, out actionName, out areaName, out pagePath);

                static void GetControllerData(IDictionary<string, string> dictionary, out string s, out string actionName, out string areaName, out string pagePath)
                {
                    s = dictionary.TryGetValue("controller", out s)
                            ? s?.ToLowerInvariant()
                            : null;
                    actionName = dictionary.TryGetValue("action", out actionName)
                                     ? actionName?.ToLowerInvariant()
                                     : null;
                    areaName = dictionary.TryGetValue("area", out areaName)
                                    ? areaName?.ToLowerInvariant()
                                    : null;
                    pagePath = dictionary.TryGetValue("page", out pagePath)
                                   ? pagePath?.ToLowerInvariant()
                                   : null;
                }
            }

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

                    resourceName = $"{((AspNetCoreEndpointTags)parentSpan.Tags).HttpMethod} {request.PathBase.ToUriComponent()}{resourcePathName}";

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
                // This is only called with new route names, so parent tags are always AspNetCoreEndpointTags
                var parentTags = (AspNetCoreEndpointTags)parentSpan.Tags;

                // If we're using endpoint routing or this is a pipeline re-execution,
                // these will already be set correctly
                parentTags.AspNetCoreRoute = aspNetRoute;
                parentSpan.ResourceName = span.ResourceName;
                parentTags.HttpRoute = aspNetRoute;
            }

            return span;
        }

#if NETCOREAPP
        private void OnNextSlow(string eventName, object arg)
        {
            var lastChar = eventName[^1];
            if (lastChar == 't')
            {
                if ((_eventNameCacheFlags & 1) == 0 && eventName.AsSpan().Slice(PrefixLength) is "Hosting.HttpRequestIn.Start")
                {
                    _hostingHttpRequestInStartEventKey = eventName;
                    _eventNameCacheFlags |= 1;
                    OnHostingHttpRequestInStart(arg);
                }

                return;
            }

            if (lastChar == 'n')
            {
                var suffix = eventName.AsSpan().Slice(PrefixLength);

                if ((_eventNameCacheFlags & 2) == 0 && suffix is "Mvc.BeforeAction")
                {
                    _mvcBeforeActionEventKey = eventName;
                    _eventNameCacheFlags |= 2;
                    OnMvcBeforeAction(arg);
                }
                else if ((_eventNameCacheFlags & 4) == 0 && suffix is "Mvc.AfterAction")
                {
                    _mvcAfterActionEventKey = eventName;
                    _eventNameCacheFlags |= 4;
                    OnMvcAfterAction(arg);
                }
                else if ((_eventNameCacheFlags & 8) == 0 && suffix is "Hosting.UnhandledException")
                {
                    _hostingUnhandledExceptionEventKey = eventName;
                    _eventNameCacheFlags |= 8;
                    OnHostingUnhandledException(arg);
                }
                else if ((_eventNameCacheFlags & 16) == 0 && suffix is "Diagnostics.UnhandledException")
                {
                    _diagnosticsUnhandledExceptionEventKey = eventName;
                    _eventNameCacheFlags |= 16;
                    OnHostingUnhandledException(arg);
                }

                return;
            }

            if (lastChar == 'p')
            {
                if ((_eventNameCacheFlags & 32) == 0 && eventName.AsSpan().Slice(PrefixLength) is "Hosting.HttpRequestIn.Stop")
                {
                    _hostingHttpRequestInStopEventKey = eventName;
                    _eventNameCacheFlags |= 32;
                    OnHostingHttpRequestInStop(arg);
                }

                return;
            }

            if (lastChar == 'd')
            {
                if ((_eventNameCacheFlags & 64) == 0 && eventName.AsSpan().Slice(PrefixLength) is "Routing.EndpointMatched")
                {
                    _routingEndpointMatchedKey = eventName;
                    _eventNameCacheFlags |= 64;
                    OnRoutingEndpointMatched(arg);
                }

                return;
            }
        }

#else
        private void OnNextSlow(string eventName, object arg)
        {
            var lastChar = eventName[eventName.Length - 1];

            if (lastChar == 't')
            {
                if ((_eventNameCacheFlags & 1) == 0 && eventName == "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start")
                {
                    _hostingHttpRequestInStartEventKey = eventName;
                    _eventNameCacheFlags |= 1;
                    OnHostingHttpRequestInStart(arg);
                }

                return;
            }

            if (lastChar == 'n')
            {
                if ((_eventNameCacheFlags & 2) == 0 && eventName == "Microsoft.AspNetCore.Mvc.BeforeAction")
                {
                    _mvcBeforeActionEventKey = eventName;
                    _eventNameCacheFlags |= 2;
                    OnMvcBeforeAction(arg);
                }
                else if ((_eventNameCacheFlags & 4) == 0 && eventName == "Microsoft.AspNetCore.Mvc.AfterAction")
                {
                    _mvcAfterActionEventKey = eventName;
                    _eventNameCacheFlags |= 4;
                    OnMvcAfterAction(arg);
                }
                else if ((_eventNameCacheFlags & 8) == 0 && eventName == "Microsoft.AspNetCore.Hosting.UnhandledException")
                {
                    _hostingUnhandledExceptionEventKey = eventName;
                    _eventNameCacheFlags |= 8;
                    OnHostingUnhandledException(arg);
                }
                else if ((_eventNameCacheFlags & 16) == 0 && eventName == "Microsoft.AspNetCore.Diagnostics.UnhandledException")
                {
                    _diagnosticsUnhandledExceptionEventKey = eventName;
                    _eventNameCacheFlags |= 16;
                    OnHostingUnhandledException(arg);
                }

                return;
            }

            if (lastChar == 'p')
            {
                if ((_eventNameCacheFlags & 32) == 0 && eventName == "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop")
                {
                    _hostingHttpRequestInStopEventKey = eventName;
                    _eventNameCacheFlags |= 32;
                    OnHostingHttpRequestInStop(arg);
                }

                return;
            }

            if (lastChar == 'd')
            {
                if ((_eventNameCacheFlags & 64) == 0 && eventName == "Microsoft.AspNetCore.Routing.EndpointMatched")
                {
                    _routingEndpointMatchedKey = eventName;
                    _eventNameCacheFlags |= 64;
                    OnRoutingEndpointMatched(arg);
                }

                return;
            }
        }
#endif

        private void OnHostingHttpRequestInStart(object arg)
        {
            if (!IsIntegrationEnabled)
            {
                return;
            }

            if (arg.TryDuckCast<HttpRequestInStartStruct>(out var requestStruct))
            {
                // Use an empty resource name here, as we will likely replace it as part of the request
                // If we don't, update it in OnHostingHttpRequestInStop or OnHostingUnhandledException
                var httpContext = requestStruct.HttpContext;
                var security = CurrentSecurity;
                var scope = AspNetCoreRequestHandler.StartAspNetCorePipelineScope(CurrentTracer, security, httpContext, resourceName: string.Empty);
                if (security.Enabled)
                {
                    CoreHttpContextStore.Instance.Set(httpContext);
                    SecurityCoordinator.ReportWafInitInfoOnce(security, scope.Span);
                }
            }
        }

        private void OnRoutingEndpointMatched(object arg)
        {
            var tracer = CurrentTracer;

            if (!IsIntegrationEnabled ||
                !tracer.Settings.RouteTemplateResourceNamesEnabled ||
                !arg.TryDuckCast<HttpRequestInEndpointMatchedStruct>(out var typedArg))
            {
                return;
            }

            if (tracer.InternalActiveScope?.Span is { Tags: AspNetCoreEndpointTags tags } span)
            {
                var httpContext = typedArg.HttpContext;
                var trackingFeature = httpContext.Features.Get<AspNetCoreHttpRequestHandler.RequestTrackingFeature>();
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
                string controllerName = null;
                string actionName = null;
                string areaName = null;

                // Let's avoid using TryGetValue multiple times and enumerate the Dictionary and bailout when we get all the data.
                var count = 0;
                foreach (var routeValue in routeValues)
                {
                    if (count == 3)
                    {
                        break;
                    }

                    if (string.Equals(routeValue.Key, "controller", StringComparison.OrdinalIgnoreCase))
                    {
                        controllerName = routeValue.Value as string;
                        count++;
                        continue;
                    }

                    if (string.Equals(routeValue.Key, "action", StringComparison.OrdinalIgnoreCase))
                    {
                        actionName = routeValue.Value as string;
                        count++;
                        continue;
                    }

                    if (string.Equals(routeValue.Key, "area", StringComparison.OrdinalIgnoreCase))
                    {
                        areaName = routeValue.Value as string;
                        count++;
                        continue;
                    }
                }

                var resourcePathName = AspNetCoreResourceNameHelper.SimplifyRoutePattern(
                    routePattern,
                    routeValues,
                    areaName: areaName,
                    controllerName: controllerName,
                    actionName: actionName,
                    tracer.Settings.ExpandRouteTemplatesEnabled);

                var resourceNameBuilder = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
                resourceNameBuilder
                   .Append(tags.HttpMethod)
                   .Append(' ');
                request.PathBase.FillBufferWithUriComponent(resourceNameBuilder);
                resourceNameBuilder.Append(resourcePathName);
                var resourceName = StringBuilderCache.GetStringAndRelease(resourceNameBuilder);

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
                    tags.HttpRoute = normalizedRoute;
                }

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
        }

        private void OnMvcBeforeAction(object arg)
        {
            var tracer = CurrentTracer;
            var parentSpan = tracer.InternalActiveScope?.Span;
            if (parentSpan != null && arg.TryDuckCast<BeforeActionStruct>(out var typedArg))
            {
                var httpContext = typedArg.HttpContext;
                var request = httpContext.Request;

                // NOTE: This event is the start of the action pipeline. The action has been selected, the route
                //       has been selected but no filters have run and model binding hasn't occurred.
                Span span = null;
                if (IsIntegrationEnabled)
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

                var security = CurrentSecurity;
                if (security.Enabled)
                {
                    security.CheckPathParamsFromAction(httpContext, span, typedArg.ActionDescriptor?.Parameters, typedArg.RouteData.Values);
                }

                if (Iast.Iast.Instance.Settings.Enabled)
                {
                    parentSpan.Context?.TraceContext?.IastRequestContext?.AddRequestData(request, typedArg.RouteData?.Values);
                }
            }
        }

        private void OnMvcAfterAction(object arg)
        {
            var tracer = CurrentTracer;

            if (!IsIntegrationEnabled ||
                !tracer.Settings.RouteTemplateResourceNamesEnabled)
            {
                return;
            }

            var activeScope = tracer.InternalActiveScope;
            if (activeScope is not null && ReferenceEquals(activeScope.Span.OperationName, MvcOperationName))
            {
                // Extract data from the Activity
                if (Activity.ActivityListener.GetCurrentActivity() is { } activity)
                {
                    // If the activity listener is enabled and we have an activity we copy the info to the span.
                    SetActivityInfo(activity, activeScope);
                }

                activeScope.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void SetActivityInfo(IActivity activity, Scope scope)
        {
            try
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
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting activity data.");
            }
        }

        private void OnHostingHttpRequestInStop(object arg)
        {
            var tracer = CurrentTracer;

            if (!IsIntegrationEnabled)
            {
                return;
            }

            if (tracer.InternalActiveScope is { } scope)
            {
                AspNetCoreRequestHandler.StopAspNetCorePipelineScope(tracer, CurrentSecurity, scope, arg.DuckCast<HttpRequestInStopStruct>().HttpContext);
            }
        }

        private void OnHostingUnhandledException(object arg)
        {
            var tracer = CurrentTracer;

            if (!IsIntegrationEnabled)
            {
                return;
            }

            var span = tracer.InternalActiveScope?.Span;

            if (span != null && arg.TryDuckCast<UnhandledExceptionStruct>(out var unhandledStruct))
            {
                AspNetCoreRequestHandler.HandleAspNetCoreException(tracer, CurrentSecurity, span, unhandledStruct.HttpContext, unhandledStruct.Exception);
            }
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
