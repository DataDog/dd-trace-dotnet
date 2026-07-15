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
        internal const string HttpContextScopeKey = "__Datadog.LegacyAspNetCoreDiagnosticObserver.Scope";

        private const string DiagnosticListenerName = "Microsoft.AspNetCore";
        private const string HostingHttpRequestInOperation = "Microsoft.AspNetCore.Hosting.HttpRequestIn";
        private const string HostingHttpRequestInStartEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start";
        private const string HostingHttpRequestInStopEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop";
        private const string HostingUnhandledExceptionEvent = "Microsoft.AspNetCore.Hosting.UnhandledException";
        private const string DiagnosticsUnhandledExceptionEvent = "Microsoft.AspNetCore.Diagnostics.UnhandledException";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<LegacyAspNetCoreDiagnosticObserver>();
        private static readonly LegacyAspNetCoreHttpRequestHandler RequestHandler = new(Log);

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
             || eventData.HttpContext is null)
            {
                return;
            }

            var httpContext = eventData.HttpContext.DuckCast<LegacyAspNetCoreHttpContextStruct>();
            if (httpContext.Items.ContainsKey(HttpContextScopeKey))
            {
                return;
            }

            Scope? scope = null;
            try
            {
                var request = httpContext.Request.DuckCast<LegacyAspNetCoreHttpRequestStruct>();
                scope = RequestHandler.StartAspNetCorePipelineScope(_tracer, request);
                httpContext.Items[HttpContextScopeKey] = scope;
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
             || eventData.HttpContext is null)
            {
                return;
            }

            var httpContext = eventData.HttpContext.DuckCast<LegacyAspNetCoreHttpContextStruct>();
            if (!httpContext.Items.TryGetValue(HttpContextScopeKey, out var value) || value is not Scope scope)
            {
                return;
            }

            // Remove the exact scope before closing it so duplicate stop events cannot finish it twice.
            httpContext.Items.Remove(HttpContextScopeKey);
            try
            {
                if (httpContext.Response.TryDuckCast<LegacyAspNetCoreHttpResponseStruct>(out var response))
                {
                    RequestHandler.StopAspNetCorePipelineScope(_tracer, scope, response);
                }
            }
            finally
            {
                scope.Dispose();
            }
        }

        private void OnHostingUnhandledException(object arg)
        {
            if (!arg.TryDuckCast<LegacyAspNetCoreUnhandledExceptionStruct>(out var eventData)
             || eventData.HttpContext is null
             || eventData.Exception is null)
            {
                return;
            }

            var httpContext = eventData.HttpContext.DuckCast<LegacyAspNetCoreHttpContextStruct>();
            if (httpContext.Items.TryGetValue(HttpContextScopeKey, out var value) && value is Scope scope)
            {
                RequestHandler.HandleAspNetCoreException(_tracer, scope, eventData.Exception);
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
        internal struct LegacyAspNetCoreHttpContextStruct
        {
            public object Request;
            public object Response;
            public IDictionary<object, object> Items;
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
            public object Headers;
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
