// <copyright file="Distribution.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Telemetry.Metrics;

[SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1134:Attributes should not share line", Justification = "It's easier to read")]
[SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1124:Do not use regions", Justification = "It's easier to read")]
[TelemetryMetricType(TelemetryMetricType.Distribution)]
internal enum Distribution
{
#region General Namespace

    /// <summary>
    /// The time it takes to import/initialize the tracer on startup. If this consists of multiple steps/components, tagged by the component/total
    /// </summary>
    [TelemetryMetric("init_time", 1)] InitTime,
#endregion

// These metrics are all desirable, but are not feasible until the API accepts dd-sketches for distributions

// #region Tracers Namespace
//
//     /// <summary>
//     /// The number of spans in the trace when finished
//     /// </summary>
//     [TelemetryMetric("trace_size", 0)] TraceSize,
//
//     /// <summary>
//     /// The size in bytes of the serialized trace
//     /// </summary>
//     [TelemetryMetric("trace_serialization.bytes", 0)] TraceSerializationBytes,
//
//     /// <summary>
//     /// The time it takes to serialize a trace
//     /// </summary>
//     [TelemetryMetric("trace_serialization.ms", 0)] TraceSerializationMs,
//
//     /// <summary>
//     /// The size of the payload sent to the endpoint in bytes
//     /// </summary>
//     [TelemetryMetric("trace_api.requests.bytes", 0)] TraceApiRequestsBytes,
//
//     /// <summary>
//     /// The time it takes to send the payload sent to the endpoint in ms
//     /// </summary>
//     [TelemetryMetric("trace_api.requests.ms", 0)] TraceApiRequestsMs,
//
//     /// <summary>
//     /// The time it takes to flush the trace payload to the agent. Note that this is not the "per trace" time, this is the "per payload" time
//     /// </summary>
//     [TelemetryMetric("trace_api.ms", 0)] TraceApiMs,
//
//     /// <summary>
//     /// The size of the payload sent to the stats endpoint in bytes
//     /// </summary>
//     [TelemetryMetric("stats_api.requests.bytes", 0)] StatsApiRequestsBytes,
//
//     /// <summary>
//     /// The size of the payload sent to the stats endpoint in milliseconds
//     /// </summary>
//     [TelemetryMetric("stats_api.requests.ms", 0)] StatsApiRequestsMs,
// #endregion
// #region Telemetry Namespace
//
//     /// <summary>
//     /// The size of the payload sent to the stats endpoint in bytes, tagged by the endpoint (Agent, Agentless)
//     /// </summary>
//     [TelemetryMetric("telemetry_api.requests.bytes", 1)] TelemetryApiRequestsBytes,
//
//     /// <summary>
//     /// The time to send to the stats endpoint in milliseconds, tagged by the endpoint (Agent, Agentless)
//     /// </summary>
//     [TelemetryMetric("telemetry_api.requests.ms", 1)] TelemetryApiRequestsMs,
// #endregion
// #region .NET Namespace
//
//     /// <summary>
//     /// The number of logs included in a payload patch, sent to the submission endpoint
//     /// </summary>
//     [TelemetryMetric("direct_log_api_batch_size", 0, isCommon: false)] DirectLogApiBatchSize,
//
//     /// <summary>
//     /// The size of the payload sent to the direct log submission endpoint in bytes, regardless of success
//     /// </summary>
//     [TelemetryMetric("direct_log_api.requests.bytes", 0, isCommon: false)] DirectLogApiRequestsBytes,
//
//     /// <summary>
//     /// The time to send the payload to the direct log submission endpoint in ms, regardless of success
//     /// </summary>
//     [TelemetryMetric("direct_log_api.requests.ms", 0, isCommon: false)] DirectLogApiRequestsMs,
// #endregion
}
