// <copyright file="CorrelationIdentifier.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace
{
    /// <summary>
    /// An API to access identifying values of the service and the active span
    /// </summary>
    public static class CorrelationIdentifier
    {
        internal static readonly string ServiceKey = "dd.service";
        internal static readonly string VersionKey = "dd.version";
        internal static readonly string EnvKey = "dd.env";
        internal static readonly string TraceIdKey = "dd.trace_id";
        internal static readonly string SpanIdKey = "dd.span_id";

        // Serilog property names require valid C# identifiers
        internal static readonly string SerilogServiceKey = "dd_service";
        internal static readonly string SerilogVersionKey = "dd_version";
        internal static readonly string SerilogEnvKey = "dd_env";
        internal static readonly string SerilogTraceIdKey = "dd_trace_id";
        internal static readonly string SerilogSpanIdKey = "dd_span_id";

        /// <summary>
        /// Gets the name of the service
        /// </summary>
        public static string Service
        {
            get
            {
                return Tracer.Instance.DefaultServiceName ?? string.Empty;
            }
        }

        /// <summary>
        /// Gets the version of the service
        /// </summary>
        public static string Version
        {
            get
            {
                return Tracer.Instance.Settings.ServiceVersion ?? string.Empty;
            }
        }

        /// <summary>
        /// Gets the environment name of the service
        /// </summary>
        public static string Env
        {
            get
            {
                return Tracer.Instance.Settings.Environment ?? string.Empty;
            }
        }

        /// <summary>
        /// Gets the id of the active trace.
        /// </summary>
        /// <returns>The id of the active trace. If there is no active trace, returns zero.</returns>
        public static ulong TraceId
        {
            get
            {
                return Tracer.Instance.ActiveScope?.Span?.TraceId ?? 0;
            }
        }

        /// <summary>
        /// Gets the id of the active span.
        /// </summary>
        /// <returns>The id of the active span. If there is no active span, returns zero.</returns>
        public static ulong SpanId
        {
            get
            {
                return Tracer.Instance.ActiveScope?.Span?.SpanId ?? 0;
            }
        }
    }
}
