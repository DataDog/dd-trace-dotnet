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

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<LegacyAspNetCoreDiagnosticObserver>();
        private static readonly LegacyAspNetCoreHttpRequestHandler RequestHandler = new(Log);
        private static readonly object HttpContextRequestStateKey = new();

        private readonly Tracer _tracer;

        public LegacyAspNetCoreDiagnosticObserver(Tracer tracer)
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
         || eventName == DiagnosticsUnhandledExceptionEvent;

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
        }

        private void OnHostingHttpRequestInStart(object arg)
        {
            if (!_tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId)
             || !arg.TryDuckCast<LegacyAspNetCoreHttpRequestInEventStruct>(out var eventData)
             || eventData.HttpContext is null
             || !eventData.HttpContext.TryDuckCast<LegacyAspNetCoreHttpContextItemsStruct>(out var itemsContext)
             || itemsContext.Items is null
             || !eventData.HttpContext.TryDuckCast<LegacyAspNetCoreHttpContextRequestStruct>(out var requestContext)
             || requestContext.Request is null
             || !requestContext.Request.TryDuckCast<LegacyAspNetCoreHttpRequestStruct>(out var request)
             || request.Headers is null
             || !request.Headers.TryDuckCast<ILegacyAspNetCoreHeaders>(out var headers))
            {
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
            if (!arg.TryDuckCast<LegacyAspNetCoreHttpRequestInEventStruct>(out var eventData)
             || eventData.HttpContext is null
             || !eventData.HttpContext.TryDuckCast<LegacyAspNetCoreHttpContextItemsStruct>(out var itemsContext)
             || itemsContext.Items is null)
            {
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
                if (eventData.HttpContext.TryDuckCast<LegacyAspNetCoreHttpContextResponseStruct>(out var responseContext)
                 && responseContext.Response is not null
                 && responseContext.Response.TryDuckCast<LegacyAspNetCoreHttpResponseStruct>(out var response))
                {
                    RequestHandler.StopAspNetCorePipelineScope(_tracer, state.RootScope, response);
                }
            }
            finally
            {
                state.RootScope.Dispose();
            }
        }

        private void OnHostingUnhandledException(object arg)
        {
            if (!arg.TryDuckCast<LegacyAspNetCoreUnhandledExceptionStruct>(out var eventData)
             || eventData.HttpContext is null
             || eventData.Exception is null
             || !eventData.HttpContext.TryDuckCast<LegacyAspNetCoreHttpContextItemsStruct>(out var itemsContext)
             || itemsContext.Items is null)
            {
                return;
            }

            if (itemsContext.Items.TryGetValue(HttpContextRequestStateKey, out var value) && value is LegacyAspNetCoreRequestState state)
            {
                RequestHandler.HandleAspNetCoreException(_tracer, state.RootScope, eventData.Exception);
            }
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
            public int StatusCode;
        }
    }
}

#endif
