// <copyright file="CountCIVisibility.cs" company="Datadog">
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
[TelemetryMetricType(TelemetryMetricType.Count, isCiVisibilityMetric: true, isApmMetric: false)]
internal enum CountCIVisibility
{
    /// <summary>
    /// The number of events created by CI Visibility
    /// </summary>
    [TelemetryMetric<
        MetricTags.CIVisibilityTestFramework,
        MetricTags.CIVisibilityTestingEventTypeWithCodeOwnerAndSupportedCiAndBenchmark>
        ("event_created", isCommon: true, NS.CIVisibility)] EventCreated,

    /// <summary>
    /// The number of events finished by CI Visibility
    /// </summary>
    [TelemetryMetric<
        MetricTags.CIVisibilityTestFramework,
        MetricTags.CIVisibilityTestingEventTypeWithCodeOwnerAndSupportedCiAndBenchmark>
        ("event_finished", isCommon: true, NS.CIVisibility)] EventFinished,

    /// <summary>
    /// The number of code coverage start calls by CI Visibility
    /// </summary>
    [TelemetryMetric<
            MetricTags.CIVisibilityTestFramework,
            MetricTags.CIVisibilityCoverageLibrary>
        ("code_coverage_started", isCommon: true, NS.CIVisibility)] CodeCoverageStarted,

    /// <summary>
    /// The number of code coverage finished calls by CI Visibility
    /// </summary>
    [TelemetryMetric<
            MetricTags.CIVisibilityTestFramework,
            MetricTags.CIVisibilityCoverageLibrary>
        ("code_coverage_finished", isCommon: true, NS.CIVisibility)] CodeCoverageFinished,

    /// <summary>
    /// The number of manual api calls by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityTestingEventType>
        ("manual_api_events", isCommon: true, NS.CIVisibility)] ManualApiEvent,

    /// <summary>
    /// The number events enqueued for serialization by CI Visibility
    /// </summary>
    [TelemetryMetric("events_enqueued_for_serialization", isCommon: true, NS.CIVisibility)] EventsEnqueueForSerialization,

    /// <summary>
    /// The number of requests sent to the endpoint by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityEndpoints>
        ("endpoint_payload.requests", isCommon: true, NS.CIVisibility)] EndpointPayloadRequests,

    /// <summary>
    /// The number of code coverage finished calls by CI Visibility
    /// </summary>
    [TelemetryMetric<
            MetricTags.CIVisibilityEndpoints,
            MetricTags.CIVisibilityErrorType>
        ("endpoint_payload.requests_errors", isCommon: true, NS.CIVisibility)] EndpointPayloadRequestsErrors,

    /// <summary>
    /// The number of git commands executed by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityCommands>
        ("git.command", isCommon: true, NS.CIVisibility)] GitCommand,

    /// <summary>
    /// The number of git command that errored by CI Visibility
    /// </summary>
    [TelemetryMetric<
            MetricTags.CIVisibilityCommands,
            MetricTags.CIVisibilityExitCodes>
        ("git.command_errors", isCommon: true, NS.CIVisibility)] GitCommandErrors,

    /// <summary>
    /// The number of requests sent to the search commit endpoint by CI Visibility
    /// </summary>
    [TelemetryMetric("git_requests.search_commits", isCommon: true, NS.CIVisibility)] GitRequestsSearchCommits,

    /// <summary>
    /// The number of search commit requests sent to the endpoint that errored by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityErrorType>
        ("git_requests.search_commits_errors", isCommon: true, NS.CIVisibility)] GitRequestsSearchCommitsErrors,

    /// <summary>
    /// The number of requests sent to the git object pack endpoint by CI Visibility
    /// </summary>
    [TelemetryMetric("git_requests.objects_pack", isCommon: true, NS.CIVisibility)] GitRequestsObjectsPack,

    /// <summary>
    /// The number of git object pack requests sent to the endpoint that errored by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityErrorType>
        ("git_requests.objects_pack_errors", isCommon: true, NS.CIVisibility)] GitRequestsObjectsPackErrors,

    /// <summary>
    /// The number of requests sent to the settings endpoint, regardless of success by CI Visibility
    /// </summary>
    [TelemetryMetric("git_requests.settings", isCommon: true, NS.CIVisibility)] GitRequestsSettings,

    /// <summary>
    /// The number of settings requests sent to the endpoint that errored by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityErrorType>
        ("git_requests.settings_errors", isCommon: true, NS.CIVisibility)] GitRequestsSettingsErrors,

    /// <summary>
    /// The number of settings responses from the endpoint by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityITRSettingsResponse>
        ("git_requests.settings_response", isCommon: true, NS.CIVisibility)] GitRequestsSettingsResponse,

    /// <summary>
    /// The number of requests sent to the itr skippable tests endpoint by CI Visibility
    /// </summary>
    [TelemetryMetric("itr_skippable_tests.request", isCommon: true, NS.CIVisibility)] ITRSkippableTestsRequest,

    /// <summary>
    /// The number of itr skippable tests requests sent to the endpoint that errored by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityErrorType>
        ("itr_skippable_tests.request_errors", isCommon: true, NS.CIVisibility)] ITRSkippableTestsRequestErrors,

    /// <summary>
    /// The number of tests to skip returned by the endpoint by CI Visibility
    /// </summary>
    [TelemetryMetric("itr_skippable_tests.response_tests", isCommon: true, NS.CIVisibility)] ITRSkippableTestsResponseTests,

    /// <summary>
    /// The number of suites to skip returned by the endpoint by CI Visibility
    /// </summary>
    [TelemetryMetric("itr_skippable_tests.response_suites", isCommon: true, NS.CIVisibility)] ITRSkippableTestsResponseSuites,

    /// <summary>
    /// The number of tests or test suites skipped by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityTestingEventType>
        ("itr_skipped", isCommon: true, NS.CIVisibility)] ITRSkipped,

    /// <summary>
    /// The number of tests or test suites that are seen as unskippable by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityTestingEventType>
        ("itr_unskippable", isCommon: true, NS.CIVisibility)] ITRUnskippable,

    /// <summary>
    /// The number of tests or test suites that would've been skipped by ITR but were forced to run because of their unskippable status by CI Visibility
    /// </summary>
    [TelemetryMetric<MetricTags.CIVisibilityTestingEventType>
        ("itr_forced_run", isCommon: true, NS.CIVisibility)] ITRForcedRun,

    /// <summary>
    /// The number of successfully collected code coverages that are empty by CI Visibility
    /// </summary>
    [TelemetryMetric("code_coverage.is_empty", isCommon: true, NS.CIVisibility)] CodeCoverageIsEmpty,

    /// <summary>
    /// The number of errors while processing code coverage by CI Visibility
    /// </summary>
    [TelemetryMetric("code_coverage.errors", isCommon: true, NS.CIVisibility)] CodeCoverageErrors,
}
