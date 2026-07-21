// <copyright file="LegacyAspNetCoreDiagnosticObserver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;

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

        if (!items.TryGetValue(HttpContextRequestStateKey, out var value) || value is not LegacyAspNetCoreRequestState state)
        {
            return;
        }

        var routeDataValues = eventData.RouteData?.Values;
        var routeTemplate = action?.AttributeRouteInfo?.Template;
        var routeValues = action?.RouteValues;

        var controllerName = GetRouteValue("controller", routeValues, routeDataValues);
        var actionName = GetRouteValue("action", routeValues, routeDataValues);
        var areaName = GetRouteValue("area", routeValues, routeDataValues);

        var rootSpan = state.RootScope.Span;
        rootSpan.SetTag(Tags.AspNetCoreController, controllerName);
        rootSpan.SetTag(Tags.AspNetCoreAction, actionName);
        rootSpan.SetTag(Tags.AspNetCoreArea, areaName);

        if (routeTemplate is null && controllerName is not null && actionName is not null)
        {
            routeTemplate = areaName is null
                                ? $"{controllerName}/{actionName}"
                                : $"{areaName}/{controllerName}/{actionName}";
        }

        // If neither MVC naming source is usable, retain the normalized path resource assigned at Start.
        if (routeTemplate is null)
        {
            return;
        }

        var httpMethod = httpContext.Request.Method?.ToUpperInvariant() ?? "UNKNOWN";
        rootSpan.ResourceName = $"{httpMethod} {routeTemplate}";
        rootSpan.SetTag(Tags.AspNetCoreRoute, routeTemplate);
    }

    private string? GetRouteValue(
        string name,
        IDictionary<string, string>? actionDescriptorValues,
        IDictionary<string, object>? routeDataValues)
    {
        if (actionDescriptorValues is not null
         && actionDescriptorValues.TryGetValue(name, out var actionDescriptorValue)
         && actionDescriptorValue is not null)
        {
            return actionDescriptorValue;
        }

        if (routeDataValues is not null
         && routeDataValues.TryGetValue(name, out var routeDataValue)
         && routeDataValue is string stringValue)
        {
            return stringValue;
        }

        return null;
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
}

#endif
