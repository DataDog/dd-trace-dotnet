// <copyright file="TelemetryMetricTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests
{
    public class TelemetryMetricTest
    {
        private static string _multiplines =
        "{\"api_version\":\"v2\",\"tracer_time\":1718895607,\"runtime_id\":\"ee1b3b5f-1d9d-46cc-9cdd-1d940a94f623\",\"seq_id\":3,\"application\":{\"service_name\":\"dd-buggybits-allocs\",\"service_version\":\"BuggyBitsVersion\",\"env\":\"apm-profiling-local\",\"language_name\":\"dotnet\",\"language_version\":\"core-6.0\",\"tracer_version\":\"2.54.0\"},\"host\":{\"hostname\":\"DESKTOP-TNGHRCP\",\"os\":\"windows\",\"os_version\":\"6.2.9200\"},\"request_type\":\"message-batch\",\"payload\":[{\"request_type\":\"generate-metrics\",\"payload\":{\"series\":[{\"namespace\":\"profilers\",\"metric\":\"ssi_heuristic.number_of_profiles\",\"points\":[[1718895567,1.0]],\"tags\":[\"has_sent_profiles:true\",\"heuristic_hypothetical_decision:short_lived\",\"installation:ssi\",\"enablement_choice:manually_enabled\"],\"common\":true,\"type\":\"count\",\"interval\":10},{\"namespace\":\"profilers\",\"metric\":\"ssi_heuristic.number_of_profiles\",\"points\":[[1718895597,1.0]],\"tags\":[\"has_sent_profiles:true\",\"heuristic_hypothetical_decision:triggered\",\"installation:ssi\",\"enablement_choice:manually_enabled\"],\"common\":true,\"type\":\"count\",\"interval\":10}]}}]}" + "\n" +
        "{\"api_version\":\"v2\",\"tracer_time\":1718895667,\"runtime_id\":\"ee1b3b5f-1d9d-46cc-9cdd-1d940a94f623\",\"seq_id\":5,\"application\":{\"service_name\":\"dd-buggybits-allocs\",\"service_version\":\"BuggyBitsVersion\",\"env\":\"apm-profiling-local\",\"language_name\":\"dotnet\",\"language_version\":\"core-6.0\",\"tracer_version\":\"2.54.0\"},\"host\":{\"hostname\":\"DESKTOP-TNGHRCP\",\"os\":\"windows\",\"os_version\":\"6.2.9200\"},\"request_type\":\"message-batch\",\"payload\":[{\"request_type\":\"generate-metrics\",\"payload\":{\"series\":[{\"namespace\":\"profilers\",\"metric\":\"ssi_heuristic.number_of_profiles\",\"points\":[[1718895617,1.0],[1718895637,1.0]],\"tags\":[\"has_sent_profiles:true\",\"heuristic_hypothetical_decision:triggered\",\"installation:ssi\",\"enablement_choice:manually_enabled\"],\"common\":true,\"type\":\"count\",\"interval\":10}]}}]}" + "\n" +
        "{\"api_version\":\"v2\",\"tracer_time\":1718895727,\"runtime_id\":\"ee1b3b5f-1d9d-46cc-9cdd-1d940a94f623\",\"seq_id\":7,\"application\":{\"service_name\":\"dd-buggybits-allocs\",\"service_version\":\"BuggyBitsVersion\",\"env\":\"apm-profiling-local\",\"language_name\":\"dotnet\",\"language_version\":\"core-6.0\",\"tracer_version\":\"2.54.0\"},\"host\":{\"hostname\":\"DESKTOP-TNGHRCP\",\"os\":\"windows\",\"os_version\":\"6.2.9200\"},\"request_type\":\"message-batch\",\"payload\":[{\"request_type\":\"generate-metrics\",\"payload\":{\"series\":[{\"namespace\":\"profilers\",\"metric\":\"ssi_heuristic.number_of_profiles\",\"points\":[[1718895667,1.0],[1718895687,1.0],[1718895707,1.0]],\"tags\":[\"has_sent_profiles:true\",\"heuristic_hypothetical_decision:triggered\",\"installation:ssi\",\"enablement_choice:manually_enabled\"],\"common\":true,\"type\":\"count\",\"interval\":10}]}}]}" + "\n" +
        "{\"api_version\":\"v2\",\"tracer_time\":1718895787,\"runtime_id\":\"ee1b3b5f-1d9d-46cc-9cdd-1d940a94f623\",\"seq_id\":9,\"application\":{\"service_name\":\"dd-buggybits-allocs\",\"service_version\":\"BuggyBitsVersion\",\"env\":\"apm-profiling-local\",\"language_name\":\"dotnet\",\"language_version\":\"core-6.0\",\"tracer_version\":\"2.54.0\"},\"host\":{\"hostname\":\"DESKTOP-TNGHRCP\",\"os\":\"windows\",\"os_version\":\"6.2.9200\"},\"request_type\":\"message-batch\",\"payload\":[{\"request_type\":\"generate-metrics\",\"payload\":{\"series\":[{\"namespace\":\"profilers\",\"metric\":\"ssi_heuristic.number_of_profiles\",\"points\":[[1718895737,1.0],[1718895757,1.0],[1718895777,1.0]],\"tags\":[\"has_sent_profiles:true\",\"heuristic_hypothetical_decision:triggered\",\"installation:ssi\",\"enablement_choice:manually_enabled\"],\"common\":true,\"type\":\"count\",\"interval\":10}]}}]}" + "\n" +
        "{\"api_version\":\"v2\",\"tracer_time\":1718895847,\"runtime_id\":\"ee1b3b5f-1d9d-46cc-9cdd-1d940a94f623\",\"seq_id\":11,\"application\":{\"service_name\":\"dd-buggybits-allocs\",\"service_version\":\"BuggyBitsVersion\",\"env\":\"apm-profiling-local\",\"language_name\":\"dotnet\",\"language_version\":\"core-6.0\",\"tracer_version\":\"2.54.0\"},\"host\":{\"hostname\":\"DESKTOP-TNGHRCP\",\"os\":\"windows\",\"os_version\":\"6.2.9200\"},\"request_type\":\"message-batch\",\"payload\":[{\"request_type\":\"generate-metrics\",\"payload\":{\"series\":[{\"namespace\":\"profilers\",\"metric\":\"ssi_heuristic.number_of_profiles\",\"points\":[[1718895807,1.0],[1718895827,1.0]],\"tags\":[\"has_sent_profiles:true\",\"heuristic_hypothetical_decision:triggered\",\"installation:ssi\",\"enablement_choice:manually_enabled\"],\"common\":true,\"type\":\"count\",\"interval\":10}]}}]}" + "\n" +
        "{\"api_version\":\"v2\",\"tracer_time\":1718886587,\"runtime_id\":\"349a16a8-be00-47d9-bbf1-d6e3137bcefd\",\"seq_id\":3,\"application\":{\"service_name\":\"dd-dotnet-all\",\"service_version\":\"Unspecified-Version\",\"env\":\"apm-profiling-local\",\"language_name\":\"dotnet\",\"language_version\":\"core-8.0\",\"tracer_version\":\"2.54.0\"},\"host\":{\"hostname\":\"DESKTOP-TNGHRCP\",\"os\":\"windows\",\"os_version\":\"6.2.9200\"},\"request_type\":\"message-batch\",\"payload\":[{\"request_type\":\"generate-metrics\",\"payload\":{\"series\":[{\"namespace\":\"profilers\",\"metric\":\"ssi_heuristic.number_of_profiles\",\"points\":[[1718886587,1.0]],\"tags\":[\"has_sent_profiles:false\",\"heuristic_hypothetical_decision:no_span_short_lived\",\"installation:ssi\",\"enablement_choice:ssi_enabled\"],\"common\":true,\"type\":\"count\",\"interval\":10},{\"namespace\":\"profilers\",\"metric\":\"ssi_heuristic.number_of_runtime_id\",\"points\":[[1718886587,1.0]],\"tags\":[\"has_sent_profiles:false\",\"heuristic_hypothetical_decision:no_span_short_lived\",\"installation:ssi\",\"enablement_choice:ssi_enabled\"],\"common\":true,\"type\":\"count\",\"interval\":10}]}}]}" + "\n" +
        "{\"api_version\":\"v2\",\"tracer_time\":1718895907,\"runtime_id\":\"ee1b3b5f-1d9d-46cc-9cdd-1d940a94f623\",\"seq_id\":13,\"application\":{\"service_name\":\"dd-buggybits-allocs\",\"service_version\":\"BuggyBitsVersion\",\"env\":\"apm-profiling-local\",\"language_name\":\"dotnet\",\"language_version\":\"core-6.0\",\"tracer_version\":\"2.54.0\"},\"host\":{\"hostname\":\"DESKTOP-TNGHRCP\",\"os\":\"windows\",\"os_version\":\"6.2.9200\"},\"request_type\":\"message-batch\",\"payload\":[{\"request_type\":\"generate-metrics\",\"payload\":{\"series\":[{\"namespace\":\"profilers\",\"metric\":\"ssi_heuristic.number_of_profiles\",\"points\":[[1718895847,1.0],[1718895867,1.0],[1718895897,1.0]],\"tags\":[\"has_sent_profiles:true\",\"heuristic_hypothetical_decision:triggered\",\"installation:ssi\",\"enablement_choice:manually_enabled\"],\"common\":true,\"type\":\"count\",\"interval\":10}]}}]}" + "\n" +
        "{\"api_version\":\"v2\",\"tracer_time\":1718895967,\"runtime_id\":\"ee1b3b5f-1d9d-46cc-9cdd-1d940a94f623\",\"seq_id\":15,\"application\":{\"service_name\":\"dd-buggybits-allocs\",\"service_version\":\"BuggyBitsVersion\",\"env\":\"apm-profiling-local\",\"language_name\":\"dotnet\",\"language_version\":\"core-6.0\",\"tracer_version\":\"2.54.0\"},\"host\":{\"hostname\":\"DESKTOP-TNGHRCP\",\"os\":\"windows\",\"os_version\":\"6.2.9200\"},\"request_type\":\"message-batch\",\"payload\":[{\"request_type\":\"generate-metrics\",\"payload\":{\"series\":[{\"namespace\":\"profilers\",\"metric\":\"ssi_heuristic.number_of_profiles\",\"points\":[[1718895917,1.0],[1718895937,1.0]],\"tags\":[\"has_sent_profiles:true\",\"heuristic_hypothetical_decision:triggered\",\"installation:ssi\",\"enablement_choice:manually_enabled\"],\"common\":true,\"type\":\"count\",\"interval\":10}]}}]}" + "\n" +
        "{\"api_version\":\"v2\",\"tracer_time\":1718896027,\"runtime_id\":\"ee1b3b5f-1d9d-46cc-9cdd-1d940a94f623\",\"seq_id\":17,\"application\":{\"service_name\":\"dd-buggybits-allocs\",\"service_version\":\"BuggyBitsVersion\",\"env\":\"apm-profiling-local\",\"language_name\":\"dotnet\",\"language_version\":\"core-6.0\",\"tracer_version\":\"2.54.0\"},\"host\":{\"hostname\":\"DESKTOP-TNGHRCP\",\"os\":\"windows\",\"os_version\":\"6.2.9200\"},\"request_type\":\"message-batch\",\"payload\":[{\"request_type\":\"generate-metrics\",\"payload\":{\"series\":[{\"namespace\":\"profilers\",\"metric\":\"ssi_heuristic.number_of_profiles\",\"points\":[[1718895967,1.0],[1718895987,1.0],[1718896008,1.0]],\"tags\":[\"has_sent_profiles:true\",\"heuristic_hypothetical_decision:triggered\",\"installation:ssi\",\"enablement_choice:manually_enabled\"],\"common\":true,\"type\":\"count\",\"interval\":10}]}}]}"
        ;

        private readonly ITestOutputHelper _output;

        public TelemetryMetricTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void CheckNullMetricString()
        {
            var metrics = TelemetryMetricsFileParser.LoadFromString(null);
            metrics.Should().BeNull();
        }

        [Fact]
        public void CheckInvalidMetrics()
        {
            var metrics = TelemetryMetricsFileParser.LoadFromString("not the expected format");
            metrics.Should().NotBeNull();
            metrics.RuntimeIds.Count.Should().Be(0);
            metrics.Profiles.Count.Should().Be(0);
        }

        [Fact]
        public void CheckNoMetricsJson()
        {
            var metrics = TelemetryMetricsFileParser.LoadFromString("{\"api_version\":\"v2\",\"tracer_time\":1718896062,\"runtime_id\":\"9492c079-e4a9-4fd2-a172-bce240beb290\",\"seq_id\":1,\"application\":{\"service_name\":\"dd-buggybits-allocs\",\"service_version\":\"BuggyBitsVersion\",\"env\":\"apm-profiling-local\",\"language_name\":\"dotnet\",\"language_version\":\"core-6.0\",\"tracer_version\":\"2.54.0\"},\"host\":{\"hostname\":\"DESKTOP-TNGHRCP\",\"os\":\"windows\",\"os_version\":\"6.2.9200\"},\"request_type\":\"app-started\",\"payload\":{\"configuration\":[]}}");
            metrics.Should().NotBeNull();
            metrics.RuntimeIds.Count.Should().Be(0);
            metrics.Profiles.Count.Should().Be(0);
        }

        [Fact]
        public void CheckOneMetricsJson()
        {
            var metrics = TelemetryMetricsFileParser.LoadFromString("{\"api_version\":\"v2\",\"tracer_time\":1718886695,\"runtime_id\":\"54b48743-fc8c-4a20-af6b-796bc9816e40\",\"seq_id\":3,\"application\":{\"service_name\":\"dd-dotnet-all\",\"service_version\":\"Unspecified-Version\",\"env\":\"apm-profiling-local\",\"language_name\":\"dotnet\",\"language_version\":\"core-8.0\",\"tracer_version\":\"2.54.0\"},\"host\":{\"hostname\":\"DESKTOP-TNGHRCP\",\"os\":\"windows\",\"os_version\":\"6.2.9200\"},\"request_type\":\"message-batch\",\"payload\":[{\"request_type\":\"generate-metrics\",\"payload\":{\"series\":[{\"namespace\":\"profilers\",\"metric\":\"ssi_heuristic.number_of_profiles\",\"points\":[[1718886695,2.0]],\"tags\":[\"has_sent_profiles:false\",\"heuristic_hypothetical_decision:no_span_short_lived\",\"installation:ssi\",\"enablement_choice:ssi_enabled\"],\"common\":true,\"type\":\"count\",\"interval\":10},{\"namespace\":\"profilers\",\"metric\":\"ssi_heuristic.number_of_runtime_id\",\"points\":[[1718886695,1.0]],\"tags\":[\"has_sent_profiles:false\",\"heuristic_hypothetical_decision:no_span_short_lived\",\"installation:ssi\",\"enablement_choice:ssi_enabled\"],\"common\":true,\"type\":\"count\",\"interval\":10}]}}]}");
            metrics.Should().NotBeNull();
            metrics.RuntimeIds.Count.Should().Be(1);
            metrics.Profiles.Count.Should().Be(1);

            metrics.HasSentProfile().Should().BeFalse();

            var expectedTags = new[]
            {
                "heuristic_hypothetical_decision:no_span_short_lived",
                "installation:ssi",
                "enablement_choice:ssi_enabled"
            };
            metrics.ShouldContainTags(expectedTags);

            var numberOfProfilesMetric = metrics.Profiles[0];
            numberOfProfilesMetric.Should().NotBeNull();
            numberOfProfilesMetric.AssertTagsContains("installation", "ssi");
            numberOfProfilesMetric.AssertTagsContains("enablement_choice", "ssi_enabled");
            numberOfProfilesMetric.AssertTagsContains("has_sent_profiles", "false");
            numberOfProfilesMetric.AssertTagsContains("heuristic_hypothetical_decision", "no_span_short_lived");
            string error = string.Empty;
            numberOfProfilesMetric.ContainsTags(expectedTags, false, ref error).Should().BeTrue();
            numberOfProfilesMetric.ContainsTags(expectedTags, true, ref error).Should().BeFalse(); // missing has_sent_profiles:true

            var numberOfRuntimeIdMetric = metrics.RuntimeIds[0];
            numberOfRuntimeIdMetric.Should().NotBeNull();
            numberOfRuntimeIdMetric.AssertTagsContains("installation", "ssi");
            numberOfRuntimeIdMetric.AssertTagsContains("enablement_choice", "ssi_enabled");
            numberOfRuntimeIdMetric.AssertTagsContains("has_sent_profiles", "false");
            numberOfRuntimeIdMetric.AssertTagsContains("heuristic_hypothetical_decision", "no_span_short_lived");
            numberOfRuntimeIdMetric.ContainsTags(expectedTags, false, ref error).Should().BeTrue();
            numberOfRuntimeIdMetric.ContainsTags(expectedTags, true, ref error).Should().BeFalse(); // missing has_sent_profiles:true
        }

        [Fact]
        public void CheckMultiMetricsJson()
        {
            var metrics = TelemetryMetricsFileParser.LoadFromString(_multiplines);
            metrics.Should().NotBeNull();
            metrics.RuntimeIds.Count.Should().Be(1);
            metrics.Profiles.Count.Should().Be(10);
            metrics.HasSentProfile().Should().BeTrue();
        }
    }
}
