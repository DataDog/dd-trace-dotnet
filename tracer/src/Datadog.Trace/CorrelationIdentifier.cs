// <copyright file="CorrelationIdentifier.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace
{
    /// <summary>
    /// An API to access identifying values of the service and the active span
    /// </summary>
    public static class CorrelationIdentifier
    {
        internal const string ServiceKey = "dd.service";
        internal const string VersionKey = "dd.version";
        internal const string EnvKey = "dd.env";
        internal const string TraceIdKey = "dd.trace_id";
        internal const string SpanIdKey = "dd.span_id";

        // Serilog property names require valid C# identifiers
        internal const string SerilogServiceKey = "dd_service";
        internal const string SerilogVersionKey = "dd_version";
        internal const string SerilogEnvKey = "dd_env";
        internal const string SerilogTraceIdKey = "dd_trace_id";
        internal const string SerilogSpanIdKey = "dd_span_id";
    }
}
