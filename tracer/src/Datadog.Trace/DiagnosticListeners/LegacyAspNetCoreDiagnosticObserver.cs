// <copyright file="LegacyAspNetCoreDiagnosticObserver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.DiagnosticListeners.DuckTypes;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.DiagnosticListeners
{
    /// <summary>
    /// Creates ASP.NET Core request spans in .NET Framework processes without referencing ASP.NET Core assemblies.
    /// </summary>
    internal sealed class LegacyAspNetCoreDiagnosticObserver : DiagnosticObserver
    {
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.AspNetCore;

        private const string DiagnosticListenerName = "Microsoft.AspNetCore";
        private const string HostingHttpRequestInOperation = "Microsoft.AspNetCore.Hosting.HttpRequestIn";
        private const string HostingHttpRequestInStartEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start";
        private const string HostingHttpRequestInStopEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop";
        private const string HostingUnhandledExceptionEvent = "Microsoft.AspNetCore.Hosting.UnhandledException";
        private const string DiagnosticsUnhandledExceptionEvent = "Microsoft.AspNetCore.Diagnostics.UnhandledException";
        private const string MvcBeforeActionEvent = "Microsoft.AspNetCore.Mvc.BeforeAction";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<LegacyAspNetCoreDiagnosticObserver>();
        private static readonly LegacyAspNetCoreHttpRequestHandler RequestHandler = new(Log);
        private static readonly object HttpContextRequestStateKey = new();

        private readonly IDatadogLogger _log;
        private readonly IMetricsTelemetryCollector _metrics;

        private readonly Tracer _tracer;
        private int _reportedIncompatibleShapes;

        public LegacyAspNetCoreDiagnosticObserver(Tracer tracer, IMetricsTelemetryCollector metrics)
            : this(tracer, metrics, Log)
        {
        }

        internal LegacyAspNetCoreDiagnosticObserver(Tracer tracer, IMetricsTelemetryCollector metrics, IDatadogLogger log)
        {
            _tracer = tracer;
            _log = log;
            _metrics = metrics;
        }

        [Flags]
        private enum IncompatibleShape
        {
            StartEventPayload = 1 << 0,
            StartItemsContext = 1 << 1,
            StartRequestContext = 1 << 2,
            StartRequest = 1 << 3,
            StartHeaders = 1 << 4,
            StopEventPayload = 1 << 5,
            StopItemsContext = 1 << 6,
            StopResponseContext = 1 << 7,
            StopResponse = 1 << 8,
            HostingExceptionEventPayload = 1 << 9,
            HostingExceptionItemsContext = 1 << 10,
            DiagnosticsExceptionEventPayload = 1 << 11,
            DiagnosticsExceptionItemsContext = 1 << 12,
            MvcBeforeActionEventPayload = 1 << 13,
            MvcBeforeActionItemsContext = 1 << 14,
            MvcBeforeActionActionDescriptor = 1 << 15,
            MvcBeforeActionAttributeRouteInfo = 1 << 16,
            MvcBeforeActionRouteData = 1 << 17,
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
                OnHostingUnhandledException(eventName, arg);
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

            if (!arg.TryDuckCast<LegacyAspNetCoreHttpRequestInEventStruct>(out var eventData))
            {
                ReportIncompatibleShape(IncompatibleShape.StartEventPayload, HostingHttpRequestInStartEvent, nameof(LegacyAspNetCoreHttpRequestInEventStruct), arg);
                return;
            }

            if (eventData.HttpContext is null
             || !eventData.HttpContext.TryDuckCast<LegacyAspNetCoreHttpContextItemsStruct>(out var itemsContext)
             || itemsContext.Items is null)
            {
                ReportIncompatibleShape(IncompatibleShape.StartItemsContext, HostingHttpRequestInStartEvent, nameof(LegacyAspNetCoreHttpContextItemsStruct), eventData.HttpContext);
                return;
            }

            if (!eventData.HttpContext.TryDuckCast<LegacyAspNetCoreHttpContextRequestStruct>(out var requestContext)
             || requestContext.Request is null)
            {
                ReportIncompatibleShape(IncompatibleShape.StartRequestContext, HostingHttpRequestInStartEvent, nameof(LegacyAspNetCoreHttpContextRequestStruct), eventData.HttpContext);
                return;
            }

            if (!requestContext.Request.TryDuckCast<LegacyAspNetCoreHttpRequestStruct>(out var request))
            {
                ReportIncompatibleShape(IncompatibleShape.StartRequest, HostingHttpRequestInStartEvent, nameof(LegacyAspNetCoreHttpRequestStruct), requestContext.Request);
                return;
            }

            if (request.Headers is null || !request.Headers.TryDuckCast<ILegacyAspNetCoreHeaders>(out var headers))
            {
                ReportIncompatibleShape(IncompatibleShape.StartHeaders, HostingHttpRequestInStartEvent, nameof(ILegacyAspNetCoreHeaders), request.Headers);
                return;
            }

            if (itemsContext.Items.ContainsKey(HttpContextRequestStateKey))
            {
                return;
            }

            Scope? scope = null;
            try
            {
                var headersAdapter = new LegacyAspNetCoreHeadersCollectionAdapter(headers);
                scope = RequestHandler.StartAspNetCorePipelineScope(_tracer, request, headersAdapter);
                itemsContext.Items[HttpContextRequestStateKey] = new LegacyAspNetCoreRequestState(scope);
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
            if (!arg.TryDuckCast<LegacyAspNetCoreHttpRequestInEventStruct>(out var eventData))
            {
                ReportIncompatibleShape(IncompatibleShape.StopEventPayload, HostingHttpRequestInStopEvent, nameof(LegacyAspNetCoreHttpRequestInEventStruct), arg);
                return;
            }

            if (eventData.HttpContext is null
             || !eventData.HttpContext.TryDuckCast<LegacyAspNetCoreHttpContextItemsStruct>(out var itemsContext)
             || itemsContext.Items is null)
            {
                ReportIncompatibleShape(IncompatibleShape.StopItemsContext, HostingHttpRequestInStopEvent, nameof(LegacyAspNetCoreHttpContextItemsStruct), eventData.HttpContext);
                return;
            }

            if (!itemsContext.Items.TryGetValue(HttpContextRequestStateKey, out var value) || value is not LegacyAspNetCoreRequestState state)
            {
                return;
            }

            if (!state.TryComplete())
            {
                return;
            }

            try
            {
                // Remove the state before response access so duplicate stop events do no work.
                itemsContext.Items.Remove(HttpContextRequestStateKey);
                if (!eventData.HttpContext.TryDuckCast<LegacyAspNetCoreHttpContextResponseStruct>(out var responseContext))
                {
                    ReportIncompatibleShape(IncompatibleShape.StopResponseContext, HostingHttpRequestInStopEvent, nameof(LegacyAspNetCoreHttpContextResponseStruct), eventData.HttpContext);
                }
                else if (responseContext.Response is null
                      || !responseContext.Response.TryDuckCast<LegacyAspNetCoreHttpResponseStruct>(out var response))
                {
                    ReportIncompatibleShape(IncompatibleShape.StopResponse, HostingHttpRequestInStopEvent, nameof(LegacyAspNetCoreHttpResponseStruct), responseContext.Response);
                }
                else
                {
                    RequestHandler.StopAspNetCorePipelineScope(_tracer, state.RootScope, response);
                }
            }
            finally
            {
                state.RootScope.Dispose();
            }
        }

        private void OnHostingUnhandledException(string eventName, object arg)
        {
            var eventPayloadShape = eventName == HostingUnhandledExceptionEvent
                                        ? IncompatibleShape.HostingExceptionEventPayload
                                        : IncompatibleShape.DiagnosticsExceptionEventPayload;
            var itemsContextShape = eventName == HostingUnhandledExceptionEvent
                                        ? IncompatibleShape.HostingExceptionItemsContext
                                        : IncompatibleShape.DiagnosticsExceptionItemsContext;

            if (!arg.TryDuckCast<LegacyAspNetCoreUnhandledExceptionStruct>(out var eventData)
             || eventData.HttpContext is null
             || eventData.Exception is null)
            {
                ReportIncompatibleShape(eventPayloadShape, eventName, nameof(LegacyAspNetCoreUnhandledExceptionStruct), arg);
                return;
            }

            if (!eventData.HttpContext.TryDuckCast<LegacyAspNetCoreHttpContextItemsStruct>(out var itemsContext)
             || itemsContext.Items is null)
            {
                ReportIncompatibleShape(itemsContextShape, eventName, nameof(LegacyAspNetCoreHttpContextItemsStruct), eventData.HttpContext);
                return;
            }

            if (itemsContext.Items.TryGetValue(HttpContextRequestStateKey, out var value) && value is LegacyAspNetCoreRequestState state)
            {
                RequestHandler.HandleAspNetCoreException(_tracer, state.RootScope, eventData.Exception);
            }
        }

        private void OnMvcBeforeAction(object arg)
        {
            if (!arg.TryDuckCast<LegacyAspNetCoreMvcBeforeActionStruct>(out var eventData)
             || eventData.HttpContext is null)
            {
                ReportIncompatibleShape(IncompatibleShape.MvcBeforeActionEventPayload, MvcBeforeActionEvent, nameof(LegacyAspNetCoreMvcBeforeActionStruct), arg);
                return;
            }

            if (!eventData.HttpContext.TryDuckCast<LegacyAspNetCoreHttpContextItemsStruct>(out var itemsContext)
             || itemsContext.Items is null)
            {
                ReportIncompatibleShape(IncompatibleShape.MvcBeforeActionItemsContext, MvcBeforeActionEvent, nameof(LegacyAspNetCoreHttpContextItemsStruct), eventData.HttpContext);
                return;
            }

            if (!itemsContext.Items.TryGetValue(HttpContextRequestStateKey, out var value)
             || value is not LegacyAspNetCoreRequestState state)
            {
                return;
            }

            if (eventData.ActionDescriptor is null
             || !eventData.ActionDescriptor.TryDuckCast<LegacyAspNetCoreActionDescriptorStruct>(out var actionDescriptor))
            {
                ReportIncompatibleShape(IncompatibleShape.MvcBeforeActionActionDescriptor, MvcBeforeActionEvent, nameof(LegacyAspNetCoreActionDescriptorStruct), eventData.ActionDescriptor);
                return;
            }

            IDictionary<string, object>? routeDataValues = null;
            if (eventData.RouteData is not null)
            {
                if (eventData.RouteData.TryDuckCast<LegacyAspNetCoreRouteDataStruct>(out var routeData))
                {
                    routeDataValues = routeData.Values;
                }
                else
                {
                    ReportIncompatibleShape(IncompatibleShape.MvcBeforeActionRouteData, MvcBeforeActionEvent, nameof(LegacyAspNetCoreRouteDataStruct), eventData.RouteData);
                }
            }

            string? routeTemplate = null;
            if (actionDescriptor.AttributeRouteInfo is not null)
            {
                if (!actionDescriptor.AttributeRouteInfo.TryDuckCast<LegacyAspNetCoreAttributeRouteInfoStruct>(out var attributeRouteInfo))
                {
                    ReportIncompatibleShape(IncompatibleShape.MvcBeforeActionAttributeRouteInfo, MvcBeforeActionEvent, nameof(LegacyAspNetCoreAttributeRouteInfoStruct), actionDescriptor.AttributeRouteInfo);
                    return;
                }

                routeTemplate = attributeRouteInfo.Template;
            }

            var controllerName = GetRouteValue("controller", actionDescriptor.RouteValues, routeDataValues);
            var actionName = GetRouteValue("action", actionDescriptor.RouteValues, routeDataValues);
            var areaName = GetRouteValue("area", actionDescriptor.RouteValues, routeDataValues);

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

            var httpMethod = rootSpan.GetTag(Tags.HttpMethod) ?? "UNKNOWN";
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

        private void ReportIncompatibleShape(IncompatibleShape shape, string eventName, string expectedShape, object? value)
        {
            var shapeMask = (int)shape;
            int reportedShapes;
            do
            {
                reportedShapes = Interlocked.CompareExchange(ref _reportedIncompatibleShapes, 0, 0);
                if ((reportedShapes & shapeMask) != 0)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref _reportedIncompatibleShapes, reportedShapes | shapeMask, reportedShapes) != reportedShapes);

            _log.Warning(
                "ASP.NET Core diagnostic event {EventName} has an unsupported runtime shape. Expected {ExpectedShape}, actual {ActualType}. This event will not be instrumented.",
                eventName,
                expectedShape,
                value?.GetType());
            _metrics.RecordCountSharedIntegrationsError(MetricTags.IntegrationName.AspNetCore, MetricTags.InstrumentationError.DuckTyping);
        }

        [DuckCopy]
        internal struct LegacyAspNetCoreHttpRequestInEventStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public object? HttpContext;
        }

        [DuckCopy]
        internal struct LegacyAspNetCoreUnhandledExceptionStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public object? HttpContext;

            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public Exception? Exception;
        }

        [DuckCopy]
        internal struct LegacyAspNetCoreMvcBeforeActionStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public object? HttpContext;

            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public object? ActionDescriptor;

            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
            public object? RouteData;
        }

        [DuckCopy]
        internal struct LegacyAspNetCoreActionDescriptorStruct
        {
            public object? AttributeRouteInfo;
            public IDictionary<string, string>? RouteValues;
        }

        [DuckCopy]
        internal struct LegacyAspNetCoreAttributeRouteInfoStruct
        {
            public string? Template;
        }

        [DuckCopy]
        internal struct LegacyAspNetCoreRouteDataStruct
        {
            public IDictionary<string, object>? Values;
        }

        [DuckCopy]
        internal struct LegacyAspNetCoreHttpContextItemsStruct
        {
            public IDictionary<object, object>? Items;
        }

        [DuckCopy]
        internal struct LegacyAspNetCoreHttpContextRequestStruct
        {
            public object? Request;
        }

        [DuckCopy]
        internal struct LegacyAspNetCoreHttpContextResponseStruct
        {
            public object? Response;
        }

        [DuckCopy]
        internal struct LegacyAspNetCoreHttpRequestStruct
        {
            public string? Method;
            public string? Scheme;
            public LegacyAspNetCoreHostStringStruct Host;
            public LegacyAspNetCorePathStringStruct PathBase;
            public LegacyAspNetCorePathStringStruct Path;
            public LegacyAspNetCoreQueryStringStruct QueryString;
            public object? Headers;
        }

        [DuckCopy]
        internal struct LegacyAspNetCoreHttpResponseStruct
        {
            public int StatusCode;
            public object? Headers;
        }

        [DuckCopy]
        internal struct LegacyAspNetCoreHostStringStruct
        {
            public string? Value;
        }

        [DuckCopy]
        internal struct LegacyAspNetCorePathStringStruct
        {
            public string? Value;
        }

        [DuckCopy]
        internal struct LegacyAspNetCoreQueryStringStruct
        {
            public string? Value;
        }

        [DuckCopy]
        internal struct LegacyBadHttpRequestExceptionStruct
        {
            [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase | BindingFlags.NonPublic)]
            public int StatusCode;
        }
    }
}

#endif
