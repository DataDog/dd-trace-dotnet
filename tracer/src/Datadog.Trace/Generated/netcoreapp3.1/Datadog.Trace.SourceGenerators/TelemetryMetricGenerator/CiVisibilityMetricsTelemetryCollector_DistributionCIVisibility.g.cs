﻿// <copyright company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
// <auto-generated/>

#nullable enable

using System.Threading;

namespace Datadog.Trace.Telemetry;
internal partial class CiVisibilityMetricsTelemetryCollector
{
    private const int DistributionCIVisibilityLength = 48;

    /// <summary>
    /// Creates the buffer for the <see cref="Datadog.Trace.Telemetry.Metrics.DistributionCIVisibility" /> values.
    /// </summary>
    private static AggregatedDistribution[] GetDistributionCIVisibilityBuffer()
        => new AggregatedDistribution[]
        {
            // endpoint_payload.bytes, index = 0
            new(new[] { "endpoint:test_cycle" }),
            new(new[] { "endpoint:code_coverage" }),
            // endpoint_payload.requests_ms, index = 2
            new(new[] { "endpoint:test_cycle" }),
            new(new[] { "endpoint:code_coverage" }),
            // endpoint_payload.events_count, index = 4
            new(new[] { "endpoint:test_cycle" }),
            new(new[] { "endpoint:code_coverage" }),
            // endpoint_payload.events_serialization_ms, index = 6
            new(new[] { "endpoint:test_cycle" }),
            new(new[] { "endpoint:code_coverage" }),
            // git.command_ms, index = 8
            new(new[] { "command:get_repository" }),
            new(new[] { "command:get_branch" }),
            new(new[] { "command:get_remote" }),
            new(new[] { "command:get_head" }),
            new(new[] { "command:check_shallow" }),
            new(new[] { "command:unshallow" }),
            new(new[] { "command:get_local_commits" }),
            new(new[] { "command:get_objects" }),
            new(new[] { "command:pack_objects" }),
            new(new[] { "command:diff" }),
            new(new[] { "command:verify_branch_exists" }),
            new(new[] { "command:get_symbolic_ref" }),
            new(new[] { "command:show_ref" }),
            new(new[] { "command:build_candidate_list" }),
            new(new[] { "command:merge_base" }),
            new(new[] { "command:rev_list" }),
            new(new[] { "command:ls_remote" }),
            new(new[] { "command:fetch" }),
            // git_requests.search_commits_ms, index = 26
            new(null),
            new(new[] { "rs_compressed:true" }),
            // git_requests.objects_pack_ms, index = 28
            new(null),
            // git_requests.objects_pack_bytes, index = 29
            new(null),
            // git_requests.objects_pack_files, index = 30
            new(null),
            // git_requests.settings_ms, index = 31
            new(null),
            // itr_skippable_tests.request_ms, index = 32
            new(null),
            // itr_skippable_tests.response_bytes, index = 33
            new(null),
            new(new[] { "rs_compressed:true" }),
            // code_coverage.files, index = 35
            new(null),
            // known_tests.request_ms, index = 36
            new(null),
            // known_tests.response_bytes, index = 37
            new(null),
            new(new[] { "rs_compressed:true" }),
            // known_tests.response_tests, index = 39
            new(null),
            // impacted_tests_detection.request_ms, index = 40
            new(null),
            // impacted_tests_detection.response_bytes, index = 41
            new(null),
            new(new[] { "rs_compressed:true" }),
            // impacted_tests_detection.response_files, index = 43
            new(null),
            // test_management_tests.request_ms, index = 44
            new(null),
            // test_management_tests.response_bytes, index = 45
            new(null),
            new(new[] { "rs_compressed:true" }),
            // test_management_tests.response_tests, index = 47
            new(null),
        };

    /// <summary>
    /// Gets an array of metric counts, indexed by integer value of the <see cref="Datadog.Trace.Telemetry.Metrics.DistributionCIVisibility" />.
    /// Each value represents the number of unique entries in the buffer returned by <see cref="GetDistributionCIVisibilityBuffer()" />
    /// It is equal to the cardinality of the tag combinations (or 1 if there are no tags)
    /// </summary>
    private static int[] DistributionCIVisibilityEntryCounts { get; }
        = new int[]{ 2, 2, 2, 2, 18, 2, 1, 1, 1, 1, 1, 2, 1, 1, 2, 1, 1, 2, 1, 1, 2, 1, };

    public void RecordDistributionCIVisibilityEndpointPayloadBytes(Datadog.Trace.Telemetry.Metrics.MetricTags.CIVisibilityEndpoints tag, double value)
    {
        var index = 0 + (int)tag;
        _buffer.DistributionCIVisibility[index].TryEnqueue(value);
    }

    public void RecordDistributionCIVisibilityEndpointPayloadRequestsMs(Datadog.Trace.Telemetry.Metrics.MetricTags.CIVisibilityEndpoints tag, double value)
    {
        var index = 2 + (int)tag;
        _buffer.DistributionCIVisibility[index].TryEnqueue(value);
    }

    public void RecordDistributionCIVisibilityEndpointPayloadEventsCount(Datadog.Trace.Telemetry.Metrics.MetricTags.CIVisibilityEndpoints tag, double value)
    {
        var index = 4 + (int)tag;
        _buffer.DistributionCIVisibility[index].TryEnqueue(value);
    }

    public void RecordDistributionCIVisibilityEndpointEventsSerializationMs(Datadog.Trace.Telemetry.Metrics.MetricTags.CIVisibilityEndpoints tag, double value)
    {
        var index = 6 + (int)tag;
        _buffer.DistributionCIVisibility[index].TryEnqueue(value);
    }

    public void RecordDistributionCIVisibilityGitCommandMs(Datadog.Trace.Telemetry.Metrics.MetricTags.CIVisibilityCommands tag, double value)
    {
        var index = 8 + (int)tag;
        _buffer.DistributionCIVisibility[index].TryEnqueue(value);
    }

    public void RecordDistributionCIVisibilityGitRequestsSearchCommitsMs(Datadog.Trace.Telemetry.Metrics.MetricTags.CIVisibilityResponseCompressed tag, double value)
    {
        var index = 26 + (int)tag;
        _buffer.DistributionCIVisibility[index].TryEnqueue(value);
    }

    public void RecordDistributionCIVisibilityGitRequestsObjectsPackMs(double value)
    {
        _buffer.DistributionCIVisibility[28].TryEnqueue(value);
    }

    public void RecordDistributionCIVisibilityGitRequestsObjectsPackBytes(double value)
    {
        _buffer.DistributionCIVisibility[29].TryEnqueue(value);
    }

    public void RecordDistributionCIVisibilityGitRequestsObjectsPackFiles(double value)
    {
        _buffer.DistributionCIVisibility[30].TryEnqueue(value);
    }

    public void RecordDistributionCIVisibilityGitRequestsSettingsMs(double value)
    {
        _buffer.DistributionCIVisibility[31].TryEnqueue(value);
    }

    public void RecordDistributionCIVisibilityITRSkippableTestsRequestMs(double value)
    {
        _buffer.DistributionCIVisibility[32].TryEnqueue(value);
    }

    public void RecordDistributionCIVisibilityITRSkippableTestsResponseBytes(Datadog.Trace.Telemetry.Metrics.MetricTags.CIVisibilityResponseCompressed tag, double value)
    {
        var index = 33 + (int)tag;
        _buffer.DistributionCIVisibility[index].TryEnqueue(value);
    }

    public void RecordDistributionCIVisibilityCodeCoverageFiles(double value)
    {
        _buffer.DistributionCIVisibility[35].TryEnqueue(value);
    }

    public void RecordDistributionCIVisibilityKnownTestsRequestMs(double value)
    {
        _buffer.DistributionCIVisibility[36].TryEnqueue(value);
    }

    public void RecordDistributionCIVisibilityKnownTestsResponseBytes(Datadog.Trace.Telemetry.Metrics.MetricTags.CIVisibilityResponseCompressed tag, double value)
    {
        var index = 37 + (int)tag;
        _buffer.DistributionCIVisibility[index].TryEnqueue(value);
    }

    public void RecordDistributionCIVisibilityKnownTestsResponseTests(double value)
    {
        _buffer.DistributionCIVisibility[39].TryEnqueue(value);
    }

    public void RecordDistributionCIVisibilityImpactedTestsDetectionRequestMs(double value)
    {
        _buffer.DistributionCIVisibility[40].TryEnqueue(value);
    }

    public void RecordDistributionCIVisibilityImpactedTestsDetectionResponseBytes(Datadog.Trace.Telemetry.Metrics.MetricTags.CIVisibilityResponseCompressed tag, double value)
    {
        var index = 41 + (int)tag;
        _buffer.DistributionCIVisibility[index].TryEnqueue(value);
    }

    public void RecordDistributionCIVisibilityImpactedTestsDetectionResponseFiles(double value)
    {
        _buffer.DistributionCIVisibility[43].TryEnqueue(value);
    }

    public void RecordDistributionCIVisibilityTestManagementTestsRequestMs(double value)
    {
        _buffer.DistributionCIVisibility[44].TryEnqueue(value);
    }

    public void RecordDistributionCIVisibilityTestManagementTestsResponseBytes(Datadog.Trace.Telemetry.Metrics.MetricTags.CIVisibilityResponseCompressed tag, double value)
    {
        var index = 45 + (int)tag;
        _buffer.DistributionCIVisibility[index].TryEnqueue(value);
    }

    public void RecordDistributionCIVisibilityTestManagementTestsResponseTests(double value)
    {
        _buffer.DistributionCIVisibility[47].TryEnqueue(value);
    }
}