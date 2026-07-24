// <copyright file="LegacyAspNetCoreDiagnosticObserver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.DiagnosticListeners;

/// <summary>
/// Creates ASP.NET Core request spans in .NET Framework processes without referencing ASP.NET Core assemblies.
/// </summary>
internal sealed class LegacyAspNetCoreDiagnosticObserver : DiagnosticObserver
{
    internal const IntegrationId IntegrationId = Configuration.IntegrationId.AspNetCore;
    internal const string HttpContextRequestStateKey = "__Datadog.AspNetCoreHttpRequestHandler.Tracking";

    private const string DiagnosticListenerName = "Microsoft.AspNetCore";
    private const string HostingHttpRequestInOperation = "Microsoft.AspNetCore.Hosting.HttpRequestIn";
    private const string HostingHttpRequestInStartEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start";
    private const string HostingHttpRequestInStopEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop";
    private const string HostingUnhandledExceptionEvent = "Microsoft.AspNetCore.Hosting.UnhandledException";
    private const string DiagnosticsUnhandledExceptionEvent = "Microsoft.AspNetCore.Diagnostics.UnhandledException";
    private const string MvcBeforeActionEvent = "Microsoft.AspNetCore.Mvc.BeforeAction";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<LegacyAspNetCoreDiagnosticObserver>();
    private static readonly LegacyAspNetCoreHttpRequestHandler RequestHandler = new(Log);

    private readonly Tracer _tracer;
    private int _templateResolved;
    private ITemplateParserProxy? _templateParser;

    internal LegacyAspNetCoreDiagnosticObserver(Tracer tracer)
    {
        _tracer = tracer;
    }

    protected override string ListenerName => DiagnosticListenerName;

    // ASP.NET Core checks the base operation before starting its Activity, then emits request-lifecycle events.
    protected override bool IsEventEnabled(string eventName) =>
        eventName == HostingHttpRequestInOperation
     || eventName == HostingHttpRequestInStartEvent
     || eventName == HostingHttpRequestInStopEvent
     || eventName == HostingUnhandledExceptionEvent
     || eventName == DiagnosticsUnhandledExceptionEvent
     || eventName == MvcBeforeActionEvent;

    protected override void OnNext(string eventName, object arg)
    {
        if (eventName == HostingHttpRequestInStartEvent)
        {
            OnHostingHttpRequestInStart(arg);
        }
        else if (eventName == HostingHttpRequestInStopEvent)
        {
            OnHostingHttpRequestInStop(arg);
        }
        else if (eventName == HostingUnhandledExceptionEvent || eventName == DiagnosticsUnhandledExceptionEvent)
        {
            OnHostingUnhandledException(arg);
        }
        else if (eventName == MvcBeforeActionEvent)
        {
            OnMvcBeforeAction(arg);
        }
    }

    private void OnHostingHttpRequestInStart(object arg)
    {
        if (!_tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
        {
            return;
        }

        var eventData = arg.DuckCast<HttpRequestInEventStruct>();
        if (eventData.HttpContext is not { Items: { } items } httpContext)
        {
            return;
        }

        Scope? scope = null;
        try
        {
            scope = RequestHandler.StartAspNetCorePipelineScope(_tracer, httpContext.Request);
            items[HttpContextRequestStateKey] = new LegacyAspNetCoreRequestState(scope);
            scope = null;
        }
        catch
        {
            scope?.Dispose();
            throw;
        }
    }

    private void OnHostingHttpRequestInStop(object arg)
    {
        var eventData = arg.DuckCast<HttpRequestInEventStruct>();
        if (eventData.HttpContext is not { Items: { } items } httpContext)
        {
            return;
        }

        if (!items.TryGetValue(HttpContextRequestStateKey, out var value) || value is not LegacyAspNetCoreRequestState state)
        {
            return;
        }

        RequestHandler.StopAspNetCorePipelineScope(_tracer, state.RootScope, httpContext.Response);
    }

    private void OnHostingUnhandledException(object arg)
    {
        var eventData = arg.DuckCast<UnhandledExceptionStruct>();
        if (eventData.HttpContext is not { Items: { } items } || eventData.Exception is not { } exception)
        {
            return;
        }

        if (items.TryGetValue(HttpContextRequestStateKey, out var value) && value is LegacyAspNetCoreRequestState state)
        {
            RequestHandler.HandleAspNetCoreException(_tracer, state.RootScope, exception);
        }
    }

    private void OnMvcBeforeAction(object arg)
    {
        var eventData = arg.DuckCast<MvcBeforeActionStruct>();
        if (eventData is not { HttpContext: { Items: { } items } httpContext, ActionDescriptor: var action })
        {
            return;
        }

        if (!items.TryGetValue(HttpContextRequestStateKey, out var value)
         || value is not LegacyAspNetCoreRequestState state
         || state.RootScope.Span is not { Tags: AspNetCoreTags tags } rootSpan)
        {
            return;
        }

        var rawRouteTemplate = action?.AttributeRouteInfo?.Template;
        var routeDataValues = eventData.RouteData?.Values;
        var routeValues = action?.RouteValues;

        string? controllerName = routeValues?.TryGetValue("controller", out controllerName) == true
                                     ? controllerName?.ToLowerInvariant()
                                     : null;
        string? actionName = routeValues?.TryGetValue("action", out actionName) == true
                                 ? actionName?.ToLowerInvariant()
                                 : null;
        string? areaName = routeValues?.TryGetValue("area", out areaName) == true
                               ? areaName?.ToLowerInvariant()
                               : null;

        var httpMethod = httpContext.Request.Method?.ToUpperInvariant() ?? "UNKNOWN";

        RouteTemplateStruct? routeTemplate = null;
        if (rawRouteTemplate is not null)
        {
            try
            {
                var parser = _templateParser ?? GetTemplateParser(eventData.RouteData?.Routers);
                routeTemplate = parser?.Parse(rawRouteTemplate);
            }
            catch
            {
                // template parsing failures shouldn't cause crashes
            }
        }

        if (routeTemplate is null && eventData.RouteData?.Routers is { } routers)
        {
            foreach (var router in routers)
            {
                if (router.TryDuckCast<IRouteBaseProxy>(out var routeBase) && routeBase.ParsedTemplate is { } conventionalTemplate)
                {
                    routeTemplate = conventionalTemplate;
                }
            }
        }

        if (routeTemplate is { } parsedTemplate)
        {
            var resourcePathName = LegacyAspNetCoreResourceNameHelper.SimplifyRouteTemplate(
                parsedTemplate,
                routeDataValues,
                areaName: areaName,
                controllerName: controllerName,
                actionName: actionName,
                expandRouteParameters: _tracer.Settings.ExpandRouteTemplatesEnabled);

            rootSpan.ResourceName = $"{httpMethod} {httpContext.Request.PathBase.ToUriComponent()}{resourcePathName}";
            tags.AspNetCoreRoute = parsedTemplate.TemplateText?.ToLowerInvariant();
            return;
        }

        // Fallback, just use the default name
        // TODO: lazy assign resource name here, instead of on HttpStart

        ITemplateParserProxy? GetTemplateParser(IEnumerable? routers)
        {
            if (Interlocked.Exchange(ref _templateResolved, 1) != 0)
            {
                // Only try this path once as kind of expensive
                return null;
            }

            var templateParserType = TryResolveTemplateParserType(routers);
            if (templateParserType is null)
            {
                return null;
            }

            var proxyResult = DuckType.GetOrCreateProxyType(typeof(ITemplateParserProxy), templateParserType);
            if (!proxyResult.Success)
            {
                // oh no
                return null;
            }

            var proxy = (ITemplateParserProxy)proxyResult.CreateInstance(null!);
            return Interlocked.Exchange(ref _templateParser, proxy) ?? proxy;

            static Type? TryResolveTemplateParserType(IEnumerable? routers)
            {
                const string typeName = "Microsoft.AspNetCore.Routing.Template.TemplateParser";

                if (Type.GetType($"{typeName}, Microsoft.AspNetCore.Routing", throwOnError: false) is { } type)
                {
                    return type;
                }

                if (routers is null)
                {
                    return null;
                }

                foreach (var router in routers)
                {
                    if (router?.GetType().Assembly is { FullName: { } fullName } assembly
                     && fullName.StartsWith("Microsoft.AspNetCore.Routing,", StringComparison.Ordinal)
                     && assembly.GetType(typeName, throwOnError: false) is { } templateParserType)
                    {
                        return templateParserType;
                    }
                }

                return null;
            }
        }
    }

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    [DuckCopy]
    internal struct HttpRequestInEventStruct
    {
        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public HttpContextStruct? HttpContext;
    }

    [DuckCopy]
    internal struct UnhandledExceptionStruct
    {
        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public HttpContextStruct? HttpContext;

        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public Exception? Exception;
    }

    [DuckCopy]
    internal struct MvcBeforeActionStruct
    {
        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public HttpContextStruct? HttpContext;

        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public ActionDescriptorStruct? ActionDescriptor;

        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public RouteDataStruct? RouteData;
    }

    [DuckCopy]
    internal struct ActionDescriptorStruct
    {
        public AttributeRouteInfoStruct? AttributeRouteInfo;
        public IDictionary<string, string>? RouteValues;
    }

    [DuckCopy]
    internal struct AttributeRouteInfoStruct
    {
        public string? Template;
    }

    [DuckCopy]
    internal struct RouteDataStruct
    {
        public IDictionary<string, object>? Values;
        public IEnumerable? Routers;
    }

    [DuckCopy]
    internal struct HttpContextStruct
    {
        public IDictionary<object, object>? Items;
        public HttpRequestStruct Request;
        public HttpResponseStruct Response;
    }

    [DuckCopy]
    internal struct HttpRequestStruct
    {
        public string? Method;
        public string? Scheme;
        public HostStringStruct Host;
        public IPathString PathBase;
        public IPathString Path;
        public QueryStringStruct QueryString;
        public ILegacyAspNetCoreHeaders? Headers;
    }

    [DuckCopy]
    internal struct HttpResponseStruct
    {
        public int StatusCode;
        public ILegacyAspNetCoreHeaders? Headers;
    }

    [DuckCopy]
    internal struct HostStringStruct
    {
        public string? Value;
    }

#pragma warning disable SA1201 // An interface should not follow a struct
    internal interface IPathString
#pragma warning restore SA1201
    {
        public string ToUriComponent();
    }

    [DuckCopy]
    internal struct QueryStringStruct
    {
        public string? Value;
    }

    [DuckCopy]
    internal struct BadHttpRequestExceptionStruct
    {
        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase | BindingFlags.NonPublic)]
        public int StatusCode;
    }

    /// <summary>
    /// Duck-type proxy for the static <c>Microsoft.AspNetCore.Routing.Template.TemplateParser</c> type
    /// </summary>
#pragma warning disable SA1201 // An interface should not follow a struct
    internal interface ITemplateParserProxy
#pragma warning restore SA1201
    {
        RouteTemplateStruct Parse(string routeTemplate);
    }

    /// <summary>
    /// Duck-type proxy for a conventional-routing <c>Microsoft.AspNetCore.Routing.RouteBase</c> instance
    /// </summary>
#pragma warning disable SA1201 // An interface should not follow a struct
    internal interface IRouteBaseProxy
#pragma warning restore SA1201
    {
        RouteTemplateStruct? ParsedTemplate { get; }
    }

    [DuckCopy]
    internal struct RouteTemplateStruct
    {
        public string? TemplateText;
        public IEnumerable? Segments;
    }

    [DuckCopy]
    internal struct TemplateSegmentStruct
    {
        public IEnumerable? Parts;
    }

    [DuckCopy]
    internal struct TemplatePartStruct
    {
        public bool IsParameter;
        public bool IsCatchAll;
        public bool IsOptional;
        public string? Name;
        public string? Text;
    }
}

#endif
