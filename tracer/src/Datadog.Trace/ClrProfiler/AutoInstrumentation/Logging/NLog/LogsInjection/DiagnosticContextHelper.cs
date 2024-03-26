// <copyright file="DiagnosticContextHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.LogsInjection
{
    internal static class DiagnosticContextHelper
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DiagnosticContextHelper));

        private static IScopeContextSetterProxy GetScopeContextProxy(Assembly nlogAssembly)
        {
            var scType = nlogAssembly.GetType("NLog.ScopeContext");
            if (scType is not null)
            {
                var createTypeResult = DuckType.GetOrCreateProxyType(typeof(IScopeContextSetterProxy), scType);
                if (createTypeResult.Success)
                {
                    return createTypeResult.CreateInstance<IScopeContextSetterProxy>(instance: null);
                }
            }

            return null;
        }

        private static MappedDiagnosticsLogicalContextSetterProxy GetMdlcProxy(Assembly nlogAssembly)
        {
            var mdlcType = nlogAssembly.GetType("NLog.MappedDiagnosticsLogicalContext");
            if (mdlcType is not null)
            {
                // NLog 4.3+
                var createTypeResult = DuckType.GetOrCreateProxyType(typeof(MappedDiagnosticsLogicalContextSetterProxy), mdlcType);
                if (createTypeResult.Success)
                {
                    // NLog 4.6+
                    return createTypeResult.CreateInstance<MappedDiagnosticsLogicalContextSetterProxy>(instance: null);
                }
            }

            return null;
        }

        private static MappedDiagnosticsContextSetterProxy GetMdcProxy(Assembly nlogAssembly)
        {
            var mdcType = nlogAssembly.GetType("NLog.MappedDiagnosticsContext");
            if (mdcType is not null)
            {
                // NLog 2.0+
                var createTypeResult = DuckType.GetOrCreateProxyType(typeof(MappedDiagnosticsContextSetterProxy), mdcType);
                if (createTypeResult.Success)
                {
                    return createTypeResult.CreateInstance<MappedDiagnosticsContextSetterProxy>(instance: null);
                }
            }

            mdcType = nlogAssembly.GetType("NLog.MDC");
            if (mdcType is not null)
            {
                // NLog 1.0+
                var createTypeResult = DuckType.GetOrCreateProxyType(typeof(MappedDiagnosticsContextSetterProxy), mdcType);
                if (createTypeResult.Success)
                {
                    return createTypeResult.CreateInstance<MappedDiagnosticsContextSetterProxy>(instance: null);
                }
            }

            return null;
        }

        public static IDisposable SetScopeContextState(IScopeContextSetterProxy scopeContext, Tracer tracer)
        {
            var entries = CreateEntriesList(tracer, out _);
            var state = scopeContext.PushProperties(entries);

            return state;
        }

        public static bool SetMdcState(MappedDiagnosticsContextSetterProxy mdc, Tracer tracer)
        {
            var entries = CreateEntriesList(tracer, out var removeTraceIds);
            foreach (var kvp in entries)
            {
                mdc.Set(kvp.Key, (string)kvp.Value);
            }

            return removeTraceIds;
        }

        public static IDisposable SetMdlcState(MappedDiagnosticsLogicalContextSetterProxy mdlc, Tracer tracer)
        {
            var entries = CreateEntriesList(tracer, out _);
            var state = mdlc.SetScoped(entries);

            return state;
        }

        public static void RemoveMdcState(MappedDiagnosticsContextSetterProxy mdc, bool removeTraceIds)
        {
            mdc.Remove(CorrelationIdentifier.ServiceKey);
            mdc.Remove(CorrelationIdentifier.VersionKey);
            mdc.Remove(CorrelationIdentifier.EnvKey);

            if (removeTraceIds)
            {
                mdc.Remove(CorrelationIdentifier.TraceIdKey);
                mdc.Remove(CorrelationIdentifier.SpanIdKey);
            }
        }

        private static IReadOnlyList<KeyValuePair<string, object>> CreateEntriesList(Tracer tracer, out bool hasTraceIds)
        {
            hasTraceIds = false;
            var spanContext = tracer.DistributedSpanContext;

            if (spanContext is not null)
            {
                // For mismatch version support we need to keep requesting old keys.
                var hasTraceId = spanContext.TryGetValue(SpanContext.Keys.TraceId, out string traceId) ||
                                 spanContext.TryGetValue(HttpHeaderNames.TraceId, out traceId);
                var hasSpanId = spanContext.TryGetValue(SpanContext.Keys.ParentId, out string spanId) ||
                                spanContext.TryGetValue(HttpHeaderNames.ParentId, out spanId);
                if (hasTraceId && hasSpanId)
                {
                    hasTraceIds = true;
                    return new[]
                    {
                        new KeyValuePair<string, object>(CorrelationIdentifier.ServiceKey, tracer.DefaultServiceName ?? string.Empty),
                        new KeyValuePair<string, object>(CorrelationIdentifier.VersionKey, tracer.Settings.ServiceVersionInternal ?? string.Empty),
                        new KeyValuePair<string, object>(CorrelationIdentifier.EnvKey, tracer.Settings.EnvironmentInternal ?? string.Empty),
                        new KeyValuePair<string, object>(CorrelationIdentifier.TraceIdKey, traceId),
                        new KeyValuePair<string, object>(CorrelationIdentifier.SpanIdKey, spanId)
                    };
                }
            }

            return new[]
            {
                new KeyValuePair<string, object>(CorrelationIdentifier.ServiceKey, tracer.DefaultServiceName ?? string.Empty),
                new KeyValuePair<string, object>(CorrelationIdentifier.VersionKey, tracer.Settings.ServiceVersionInternal ?? string.Empty),
                new KeyValuePair<string, object>(CorrelationIdentifier.EnvKey, tracer.Settings.EnvironmentInternal ?? string.Empty)
            };
        }

        internal static class Cache<TMarker>
        {
            static Cache()
            {
                var nlogAssembly = typeof(TMarker).Assembly;

                if (GetScopeContextProxy(nlogAssembly) is { } scProxy)
                {
                    ScopeContext = scProxy;
                    return;
                }

                if (GetMdlcProxy(nlogAssembly) is { } mdlcProxy)
                {
                    Mdlc = mdlcProxy;
                    return;
                }

                if (GetMdcProxy(nlogAssembly) is { } mdcProxy)
                {
                    Mdc = mdcProxy;
                    return;
                }

                // Something is very awry, but don't throw, just don't inject logs
                Log.Warning("Failed to create proxies for MDLC, MDC and ScopeContext using TMarker={TMarker}, TMarker.Assembly={Assembly}. No automatic logs injection will occur for this assembly.", typeof(TMarker), nlogAssembly);
            }

            public static IScopeContextSetterProxy ScopeContext { get; }

            public static MappedDiagnosticsLogicalContextSetterProxy Mdlc { get; }

            public static MappedDiagnosticsContextSetterProxy Mdc { get; }
        }
    }
}
