// <copyright file="DiagnosticContextHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.LogsInjection
{
    internal static class DiagnosticContextHelper
    {
        public static MappedDiagnosticsLogicalContextSetterProxy GetMdlcProxy(Assembly nlogAssembly)
        {
            var mdclType = nlogAssembly.GetType("NLog.MappedDiagnosticsLogicalContext");
            if (mdclType is not null)
            {
                // NLog 4.3+
                var createTypeResult = DuckType.GetOrCreateProxyType(typeof(MappedDiagnosticsLogicalContextSetterProxy), mdclType);
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

            if (tracer.ActiveScope?.Span is { } span)
            {
                removeSpanId = true;
                mdc.Set(CorrelationIdentifier.TraceIdKey, span.TraceId.ToString());
                mdc.Set(CorrelationIdentifier.SpanIdKey, span.SpanId.ToString());
            }

            return removeSpanId;
        }

        public static IDisposable SetMdlcState(MappedDiagnosticsLogicalContextSetterProxy mdlc, Tracer tracer)
        {
            var span = tracer.ActiveScope?.Span;
            var array = span is null
                            ? new KeyValuePair<string, object>[3]
                            : new KeyValuePair<string, object>[5];

            array[0] = new KeyValuePair<string, object>(CorrelationIdentifier.ServiceKey, tracer.DefaultServiceName ?? string.Empty);
            array[1] = new KeyValuePair<string, object>(CorrelationIdentifier.VersionKey, tracer.Settings.ServiceVersion ?? string.Empty);
            array[2] = new KeyValuePair<string, object>(CorrelationIdentifier.EnvKey, tracer.Settings.Environment ?? string.Empty);

            if (span is not null)
            {
                array[3] = new KeyValuePair<string, object>(CorrelationIdentifier.TraceIdKey, span.TraceId);
                array[4] = new KeyValuePair<string, object>(CorrelationIdentifier.SpanIdKey, span.SpanId);
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
            }

            public static MappedDiagnosticsLogicalContextSetterProxy Mdlc { get; }

            public static MappedDiagnosticsContextSetterProxy Mdc { get; }
        }
    }
}
