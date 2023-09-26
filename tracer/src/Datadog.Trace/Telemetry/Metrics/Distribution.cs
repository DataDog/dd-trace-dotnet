// <copyright file="Distribution.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.SourceGenerators;
using NS = Datadog.Trace.Telemetry.MetricNamespaceConstants;

namespace Datadog.Trace.Telemetry.Metrics;

[SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1134:Attributes should not share line", Justification = "It's easier to read")]
[SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1124:Do not use regions", Justification = "It's easier to read")]
[TelemetryMetricType(TelemetryMetricType.Distribution)]
internal enum Distribution
{
#region General Namespace

    /// <summary>
    /// The time it takes to import/initialize the tracer on startup. If this consists of multiple steps/components, tagged by the component/total e.g. `component:call_target`. Component tags will vary by language
    /// </summary>
    [TelemetryMetric<MetricTags.InitializationComponent>("init_time", isCommon: true, NS.General)] InitTime,
#endregion
#region CIVisibility Namespace

    /// <summary>
    /// The size in bytes of the serialized payload by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityEndpoints>
        ("endpoint_payload.bytes", isCommon: true, NS.CIVisibility)] CIVisibilityEndpointPayloadBytes,

    /// <summary>
    /// The time it takes to send the payload sent to the endpoint in ms by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityEndpoints>
        ("endpoint_payload.requests_ms", isCommon: true, NS.CIVisibility)] CIVisibilityEndpointPayloadRequestsMs,

    /// <summary>
    /// The number of events included in the payload by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityEndpoints>
        ("endpoint_payload.events_count", isCommon: true, NS.CIVisibility)] CIVisibilityEndpointPayloadEventsCount,

    /// <summary>
    /// The time it takes to serialize the payload by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityEndpoints>
        ("endpoint_payload.events_serialization_ms", isCommon: true, NS.CIVisibility)] CIVisibilityEndpointEventsSerializationMs,

    /// <summary>
    /// The time it takes to execute the git command by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityCommands>
        ("git.command_ms", isCommon: true, NS.CIVisibility)] CIVisibilityGitCommandMs,

    /// <summary>
    /// The time it takes to get the response of the search commit quest in ms by CI Visibility
    /// </summary>
    [TelemetryMetric("git_requests.search_commits_ms", isCommon: true, NS.CIVisibility)] CIVisibilityGitRequestsSearchCommitsMs,

    /// <summary>
    /// The time it takes to get the response of the git object pack request in ms by CI Visibility
    /// </summary>
    [TelemetryMetric("git_requests.objects_pack_ms", isCommon: true, NS.CIVisibility)] CIVisibilityGitRequestsObjectsPackMs,

    /// <summary>
    /// The sum of the sizes of the object pack files inside a single payload by CI Visibility
    /// </summary>
    [TelemetryMetric("git_requests.objects_pack_bytes", isCommon: true, NS.CIVisibility)] CIVisibilityGitRequestsObjectsPackBytes,

    /// <summary>
    /// The number of files sent in the object pack payload by CI Visibility
    /// </summary>
    [TelemetryMetric("git_requests.objects_pack_files", isCommon: true, NS.CIVisibility)] CIVisibilityGitRequestsObjectsPackFiles,

    /// <summary>
    /// The time it takes to get the response of the settings endpoint request in ms by CI Visibility
    /// </summary>
    [TelemetryMetric("git_requests.settings_ms", isCommon: true, NS.CIVisibility)] CIVisibilityGitRequestsSettingsMs,

    /// <summary>
    /// The time it takes to get the response of the itr skippable tests endpoint request in ms by CI Visibility
    /// </summary>
    [TelemetryMetric("itr_skippable_tests.request_ms", isCommon: true, NS.CIVisibility)] CIVisibilityITRSkippableTestsRequestMs,

    /// <summary>
    /// The number of bytes received by the endpoint by CI Visibility
    /// </summary>
    [TelemetryMetric("itr_skippable_tests.response_bytes", isCommon: true, NS.CIVisibility)] CIVisibilityITRSkippableTestsResponseBytes,

    /// <summary>
    /// The number of files covered inside a coverage payload by CI Visibility
    /// </summary>
    [TelemetryMetric("code_coverage.files", isCommon: true, NS.CIVisibility)] CIVisibilityCodeCoverageFiles,

#endregion

// These metrics are all desirable, but are not feasible until the API accepts dd-sketches for distributions
// #region Tracers Namespace
//
//     /// <summary>
//     /// The number of spans in the trace chunk when it is enqueued
//     /// </summary>
//     [TelemetryMetric("trace_chunk_size")] TraceChunkSize,
//
//     /// <summary>
//     /// The size in bytes of the serialized trace chunk
//     /// </summary>
//     [TelemetryMetric("trace_chunk_serialization.bytes")] TraceChunkSerializationBytes,
//
//     /// <summary>
//     /// The time it takes to serialize a trace chunk
//     /// </summary>
//     [TelemetryMetric("trace_chunk_serialization.ms")] TraceChunkSerializationMs,
//
//     /// <summary>
//     /// The size of the payload sent to the endpoint in bytes
//     /// </summary>
//     [TelemetryMetric("trace_api.bytes")] TraceApiRequestsBytes,
//
//     /// <summary>
//     /// The time it takes to send the payload sent to the endpoint in ms
//     /// </summary>
//     [TelemetryMetric("trace_api.ms")] TraceApiRequestsMs,
//
//     /// <summary>
//     /// The time it takes to flush the trace payload to the agent. Note that this is not the per trace time, this is the per payload time
//     /// </summary>
//     [TelemetryMetric("trace_api.ms")] TraceApiMs,
//
//     /// <summary>
//     /// The number of spans included when partial flush is triggered
//     /// </summary>
//     [TelemetryMetric<MetricTags.PartialFlushReason>("trace_partial_flush.spans_closed")] TracePartialFlushSpansClosed,
//
//     /// <summary>
//     /// The number of open spans remaining in the trace when partial flush is triggered
//     /// </summary>
//     [TelemetryMetric<MetricTags.PartialFlushReason>("trace_partial_flush.spans_remaining")] TracePartialFlushSpansRemaining,
//
//     /// <summary>
//     /// The size of the payload sent to the stats endpoint in bytes
//     /// </summary>
//     [TelemetryMetric("stats_api.bytes")] StatsApiRequestsBytes,
//
//     /// <summary>
//     /// The time it takes to send the payload sent to the endpoint in ms
//     /// </summary>
//     [TelemetryMetric("stats_api.ms")] StatsApiRequestsMs,
// #endregion
// #region Telemetry Namespace
//
//     /// <summary>
//     /// The size of the payload sent to the stats endpoint in bytes, tagged by the endpoint (`endpoint:agent`, `endpoint:agentless`)
//     /// </summary>
//     [TelemetryMetric<MetricTags.TelemetryEndpoint>("telemetry_api.bytes", isCommon: true, NS.Telemetry)] TelemetryApiRequestsBytes,
//
//     /// <summary>
//     /// The time it takes to send the payload sent to the endpoint in ms, tagged by the endpoint (`endpoint:agent`, `endpoint:agentless`)
//     /// </summary>
//     [TelemetryMetric<MetricTags.TelemetryEndpoint>("telemetry_api.requests.ms", isCommon: true, NS.Telemetry)] TelemetryApiRequestsMs,
// #endregion
// #region .NET Namespace
//
//     /// <summary>
//     /// The number of logs included in a payload patch, sent to the submission endpoint
//     /// </summary>
//     [TelemetryMetric("direct_log_api.batch_size", isCommon: false)] DirectLogApiBatchSize,
//
//     /// <summary>
//     /// The size of the payload sent to the direct log submission endpoint in bytes, regardless of success
//     /// </summary>
//     [TelemetryMetric("direct_log_api.bytes", isCommon: false)] DirectLogApiRequestsBytes,
//
//     /// <summary>
//     /// The time to send the payload to the direct log submission endpoint in ms, regardless of success
//     /// </summary>
//     [TelemetryMetric("direct_log_api.ms", isCommon: false)] DirectLogApiRequestsMs,
// #endregion
}
