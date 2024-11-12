// <copyright file="DistributionCIVisibility.cs" company="Datadog">
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
[TelemetryMetricType(TelemetryMetricType.Distribution, isCiVisibilityMetric: true, isApmMetric: false)]
internal enum DistributionCIVisibility
{
    /// <summary>
    /// The size in bytes of the serialized payload by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityEndpoints>
        ("endpoint_payload.bytes", isCommon: true, NS.CIVisibility)] EndpointPayloadBytes,

    /// <summary>
    /// The time it takes to send the payload sent to the endpoint in ms by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityEndpoints>
        ("endpoint_payload.requests_ms", isCommon: true, NS.CIVisibility)] EndpointPayloadRequestsMs,

    /// <summary>
    /// The number of events included in the payload by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityEndpoints>
        ("endpoint_payload.events_count", isCommon: true, NS.CIVisibility)] EndpointPayloadEventsCount,

    /// <summary>
    /// The time it takes to serialize the payload by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityEndpoints>
        ("endpoint_payload.events_serialization_ms", isCommon: true, NS.CIVisibility)] EndpointEventsSerializationMs,

    /// <summary>
    /// The time it takes to execute the git command by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityCommands>
        ("git.command_ms", isCommon: true, NS.CIVisibility)] GitCommandMs,

    /// <summary>
    /// The time it takes to get the response of the search commit quest in ms by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityResponseCompressed>
        ("git_requests.search_commits_ms", isCommon: true, NS.CIVisibility)] GitRequestsSearchCommitsMs,

    /// <summary>
    /// The time it takes to get the response of the git object pack request in ms by CI Visibility
    /// </summary>
    [TelemetryMetric("git_requests.objects_pack_ms", isCommon: true, NS.CIVisibility)] GitRequestsObjectsPackMs,

    /// <summary>
    /// The sum of the sizes of the object pack files inside a single payload by CI Visibility
    /// </summary>
    [TelemetryMetric("git_requests.objects_pack_bytes", isCommon: true, NS.CIVisibility)] GitRequestsObjectsPackBytes,

    /// <summary>
    /// The number of files sent in the object pack payload by CI Visibility
    /// </summary>
    [TelemetryMetric("git_requests.objects_pack_files", isCommon: true, NS.CIVisibility)] GitRequestsObjectsPackFiles,

    /// <summary>
    /// The time it takes to get the response of the settings endpoint request in ms by CI Visibility
    /// </summary>
    [TelemetryMetric("git_requests.settings_ms", isCommon: true, NS.CIVisibility)] GitRequestsSettingsMs,

    /// <summary>
    /// The time it takes to get the response of the itr skippable tests endpoint request in ms by CI Visibility
    /// </summary>
    [TelemetryMetric("itr_skippable_tests.request_ms", isCommon: true, NS.CIVisibility)] ITRSkippableTestsRequestMs,

    /// <summary>
    /// The number of bytes received by the endpoint. Tagged with a boolean flag set to true if response body is compressed
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityResponseCompressed>
        ("itr_skippable_tests.response_bytes", isCommon: true, NS.CIVisibility)] ITRSkippableTestsResponseBytes,

    /// <summary>
    /// The number of files covered inside a coverage payload by CI Visibility
    /// </summary>
    [TelemetryMetric("code_coverage.files", isCommon: true, NS.CIVisibility)] CodeCoverageFiles,

    /// <summary>
    /// The time it takes to get the response of the early flake detection endpoint request in ms by CI Visibility
    /// </summary>
    [TelemetryMetric("early_flake_detection.request_ms", isCommon: true, NS.CIVisibility)] EarlyFlakeDetectionRequestMs,

    /// <summary>
    /// The number of bytes received by the endpoint. Tagged with a boolean flag set to true if response body is compressed
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityResponseCompressed>
        ("early_flake_detection.response_bytes", isCommon: true, NS.CIVisibility)] EarlyFlakeDetectionResponseBytes,

    /// <summary>
    /// The number of bytes received by the endpoint by CI Visibility
    /// </summary>
    [TelemetryMetric("early_flake_detection.response_tests", isCommon: true, NS.CIVisibility)] EarlyFlakeDetectionResponseTests,
}
