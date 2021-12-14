// <copyright file="DiagnosticContextHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.LogsInjection
{
    internal static class DiagnosticContextHelper
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DiagnosticContextHelper));

        public static MappedDiagnosticsLogicalContextSetterProxy GetMdlcProxy(Assembly nlogAssembly)
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

        public static MappedDiagnosticsContextSetterProxy GetMdcProxy(Assembly nlogAssembly)
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

        public static bool SetMdcState(MappedDiagnosticsContextSetterProxy mdc, Tracer tracer)
        {
            var removeSpanId = false;
            mdc.Set(CorrelationIdentifier.ServiceKey, tracer.DefaultServiceName ?? string.Empty);
            mdc.Set(CorrelationIdentifier.VersionKey, tracer.Settings.ServiceVersion ?? string.Empty);
            mdc.Set(CorrelationIdentifier.EnvKey, tracer.Settings.Environment ?? string.Empty);

            if (tracer.DistributedSpanContext is { } spanContext)
            {
                removeSpanId = true;
                mdc.Set(CorrelationIdentifier.TraceIdKey, spanContext[HttpHeaderNames.TraceId]);
                mdc.Set(CorrelationIdentifier.SpanIdKey, spanContext[HttpHeaderNames.ParentId]);
            }

            return removeSpanId;
        }

        public static IDisposable SetMdlcState(MappedDiagnosticsLogicalContextSetterProxy mdlc, Tracer tracer)
        {
            var spanContext = tracer.DistributedSpanContext;
            var array = spanContext is null
                            ? new KeyValuePair<string, object>[3]
                            : new KeyValuePair<string, object>[5];

            array[0] = new KeyValuePair<string, object>(CorrelationIdentifier.ServiceKey, tracer.DefaultServiceName ?? string.Empty);
            array[1] = new KeyValuePair<string, object>(CorrelationIdentifier.VersionKey, tracer.Settings.ServiceVersion ?? string.Empty);
            array[2] = new KeyValuePair<string, object>(CorrelationIdentifier.EnvKey, tracer.Settings.Environment ?? string.Empty);

            if (spanContext is not null)
            {
                array[3] = new KeyValuePair<string, object>(CorrelationIdentifier.TraceIdKey, spanContext[HttpHeaderNames.TraceId]);
                array[4] = new KeyValuePair<string, object>(CorrelationIdentifier.SpanIdKey, spanContext[HttpHeaderNames.ParentId]);
            }

            var state = mdlc.SetScoped(array);
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

        internal static class Cache<TMarker>
        {
            static Cache()
            {
                var nlogAssembly = typeof(TMarker).Assembly;

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
                Log.Warning("Failed to create proxies for both MDLC and MDC using TMarker={TMarker}, TMarker.Assembly={Assembly}. No automatic logs injection will occur for this assembly.", typeof(TMarker), nlogAssembly);
            }

            public static MappedDiagnosticsLogicalContextSetterProxy Mdlc { get; }

            public static MappedDiagnosticsContextSetterProxy Mdc { get; }
        }
    }
}
