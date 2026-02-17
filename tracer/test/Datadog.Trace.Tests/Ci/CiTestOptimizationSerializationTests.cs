// <copyright file="CiTestOptimizationSerializationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#pragma warning disable CS0649 // Field is never assigned to — struct fields populated by JSON deserialization

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

/// <summary>
/// Baseline serialization tests for CI TestOptimizationClient JSON patterns.
/// Covers: DataEnvelope/DataArrayEnvelope readonly structs with DefaultValueHandling.Ignore,
/// Settings/KnownTests/Skippable/TestManagement query/response types.
/// </summary>
public class CiTestOptimizationSerializationTests
{
    // TestOptimizationClient uses DefaultValueHandling.Ignore (different from NullValueHandling.Ignore)
    private static readonly JsonSerializerSettings SerializerSettings = new() { DefaultValueHandling = DefaultValueHandling.Ignore };

    // ===== Pattern: DataEnvelope<Data<T>> serialization with DefaultValueHandling.Ignore =====

    [Fact]
    public void DataEnvelope_Serialize_WithDefaultValueHandling()
    {
        // Pattern from TestOptimizationClient.GetSettingsAsync.cs line 83:
        // var jsonQuery = JsonConvert.SerializeObject(query, SerializerSettings);
        var envelope = new DataEnvelopeTestModel<DataTestModel<SettingsQueryTestModel>>
        {
            Data = new DataTestModel<SettingsQueryTestModel>
            {
                Id = "abc123",
                Type = "ci_app_test_service_libraries_settings",
                Attributes = new SettingsQueryTestModel
                {
                    Service = "my-service",
                    Env = "prod",
                    RepositoryUrl = "https://github.com/org/repo",
                },
            },
            Meta = new MetadataTestModel { RepositoryUrl = "https://github.com/org/repo" },
        };

        var json = JsonConvert.SerializeObject(envelope, SerializerSettings);

        json.Should().Contain("\"data\":");
        json.Should().Contain("\"meta\":");
        json.Should().Contain("\"type\":\"ci_app_test_service_libraries_settings\"");
        json.Should().Contain("\"repository_url\":\"https://github.com/org/repo\"");
        json.Should().Contain("\"service\":\"my-service\"");
        json.Should().Contain("\"env\":\"prod\"");
    }

    [Fact]
    public void DataEnvelope_DefaultValueHandling_OmitsDefaults()
    {
        // DefaultValueHandling.Ignore omits fields with default values (null, 0, false)
        var envelope = new DataEnvelopeTestModel<DataTestModel<SettingsQueryTestModel>>
        {
            Data = new DataTestModel<SettingsQueryTestModel>
            {
                Id = null, // default — should be omitted
                Type = "test",
                Attributes = default, // default — should be omitted
            },
            Meta = null, // default — should be omitted
        };

        var json = JsonConvert.SerializeObject(envelope, SerializerSettings);

        json.Should().Contain("\"type\":\"test\"");
        json.Should().NotContain("\"id\"");
        json.Should().NotContain("\"attributes\"");
        json.Should().NotContain("\"meta\"");
    }

    // ===== Pattern: DataArrayEnvelope<Data<object>> for commit search =====

    [Fact]
    public void DataArrayEnvelope_Serialize_CommitSearchRequest()
    {
        // Pattern from TestOptimizationClient.GetCommitsAsync.cs line 45:
        // JsonConvert.SerializeObject(new DataArrayEnvelope<Data<object>>(commitRequests, _repositoryUrl), SerializerSettings)
        var envelope = new DataArrayEnvelopeTestModel<DataTestModel<object>>
        {
            Data =
            [
                new DataTestModel<object> { Id = "abc123", Type = "commit" },
                new DataTestModel<object> { Id = "def456", Type = "commit" },
            ],
            Meta = new MetadataTestModel { RepositoryUrl = "https://github.com/org/repo" },
        };

        var json = JsonConvert.SerializeObject(envelope, SerializerSettings);

        json.Should().Contain("\"data\":[");
        json.Should().Contain("\"id\":\"abc123\"");
        json.Should().Contain("\"id\":\"def456\"");
        json.Should().Contain("\"type\":\"commit\"");
    }

    // ===== Pattern: SettingsResponse deserialization with many nullable bool fields =====

    [Fact]
    public void SettingsResponse_Deserialize_AllFields()
    {
        // Pattern from TestOptimizationClient.GetSettingsAsync.cs line 104
        // language=json
        var json = """
            {"data":{"type":"ci_app_libraries_tests_setting","id":"1","attributes":{
                "code_coverage":true,
                "tests_skipping":false,
                "require_git":true,
                "impacted_tests_enabled":true,
                "flaky_test_retries_enabled":false,
                "early_flake_detection":{"enabled":true,"slow_test_retries":{"5s":3,"10s":2,"30s":1,"5m":0},"faulty_session_threshold":75},
                "known_tests_enabled":true,
                "test_management":{"enabled":true,"attempt_to_fix_retries":5},
                "default_branch":"main",
                "di_enabled":false
            }}}
            """;

        var result = JsonConvert.DeserializeObject<DataEnvelopeTestModel<DataTestModel<SettingsResponseTestModel>>>(json);

        var settings = result.Data.Attributes;
        settings.CodeCoverage.Should().BeTrue();
        settings.TestsSkipping.Should().BeFalse();
        settings.RequireGit.Should().BeTrue();
        settings.ImpactedTestsEnabled.Should().BeTrue();
        settings.FlakyTestRetries.Should().BeFalse();
        settings.EarlyFlakeDetection.Enabled.Should().BeTrue();
        settings.EarlyFlakeDetection.SlowTestRetries.FiveSeconds.Should().Be(3);
        settings.EarlyFlakeDetection.SlowTestRetries.TenSeconds.Should().Be(2);
        settings.EarlyFlakeDetection.SlowTestRetries.ThirtySeconds.Should().Be(1);
        settings.EarlyFlakeDetection.SlowTestRetries.FiveMinutes.Should().Be(0);
        settings.EarlyFlakeDetection.FaultySessionThreshold.Should().Be(75);
        settings.KnownTestsEnabled.Should().BeTrue();
        settings.TestManagement.Enabled.Should().BeTrue();
        settings.TestManagement.AttemptToFixRetries.Should().Be(5);
        settings.DefaultBranch.Should().Be("main");
        settings.DynamicInstrumentationEnabled.Should().BeFalse();
    }

    [Fact]
    public void SettingsResponse_Deserialize_MissingOptionalFields()
    {
        // language=json
        var json = """{"data":{"type":"settings","attributes":{}}}""";
        var result = JsonConvert.DeserializeObject<DataEnvelopeTestModel<DataTestModel<SettingsResponseTestModel>>>(json);

        var settings = result.Data.Attributes;
        settings.CodeCoverage.Should().BeNull();
        settings.TestsSkipping.Should().BeNull();
        settings.KnownTestsEnabled.Should().BeNull();
        settings.DynamicInstrumentationEnabled.Should().BeNull();
    }

    // ===== Pattern: KnownTestsResponse — nested Dictionary<string, Dictionary<string, string[]?>> =====

    [Fact]
    public void KnownTestsResponse_Deserialize_NestedDictionaries()
    {
        // Pattern from TestOptimizationClient.GetKnownTestsAsync.cs line 64
        // language=json
        var json = """
            {"data":{"type":"ci_app_libraries_tests","attributes":{"tests":{
                "MyModule":{"MySuite":["Test1","Test2"],"OtherSuite":null}
            }}}}
            """;

        var result = JsonConvert.DeserializeObject<DataEnvelopeTestModel<DataTestModel<KnownTestsResponseTestModel>>>(json);

        var tests = result.Data.Attributes!.Tests;
        tests.Should().ContainKey("MyModule");
        tests["MyModule"].Should().ContainKey("MySuite");
        tests["MyModule"]["MySuite"].Should().Contain("Test1");
        tests["MyModule"]["MySuite"].Should().Contain("Test2");
        tests["MyModule"]["OtherSuite"].Should().BeNull();
    }

    // ===== Pattern: TestManagementResponse — deeply nested dictionaries =====

    [Fact]
    public void TestManagementResponse_Deserialize_DeepNesting()
    {
        // Pattern from TestOptimizationClient.GetTestManagementTests.cs line 65
        // language=json
        var json = """
            {"data":{"type":"test_mgmt","attributes":{"modules":{
                "MyModule":{"suites":{
                    "MySuite":{"tests":{
                        "MyTest":{"properties":{"quarantined":true,"disabled":false,"attempt_to_fix":true}}
                    }}
                }}
            }}}}
            """;

        var result = JsonConvert.DeserializeObject<DataEnvelopeTestModel<DataTestModel<TestManagementResponseTestModel>>>(json);

        var modules = result.Data.Attributes!.Modules;
        modules.Should().ContainKey("MyModule");
        var suites = modules["MyModule"].Suites;
        suites.Should().ContainKey("MySuite");
        var tests = suites["MySuite"].Tests;
        tests.Should().ContainKey("MyTest");
        var props = tests["MyTest"].Properties;
        props.Quarantined.Should().BeTrue();
        props.Disabled.Should().BeFalse();
        props.AttemptToFix.Should().BeTrue();
    }

    // ===== Pattern: SearchCommitResponse — DataArrayEnvelope deserialization =====

    [Fact]
    public void DataArrayEnvelope_Deserialize_CommitSearchResponse()
    {
        // Pattern from TestOptimizationClient.GetCommitsAsync.cs line 66:
        // JsonConvert.DeserializeObject<DataArrayEnvelope<Data<object>>>(queryResponse)
        // language=json
        var json = """{"data":[{"id":"abc123","type":"commit"},{"id":"def456","type":"commit"}]}""";
        var result = JsonConvert.DeserializeObject<DataArrayEnvelopeTestModel<DataTestModel<object>>>(json);

        result.Data.Should().HaveCount(2);
        result.Data[0].Id.Should().Be("abc123");
        result.Data[1].Id.Should().Be("def456");
    }

    // ===== Pattern: Metadata with correlation_id =====

    [Fact]
    public void Metadata_Serialize_WithCorrelationId()
    {
        var meta = new MetadataTestModel
        {
            RepositoryUrl = "https://github.com/org/repo",
            CorrelationId = "corr-12345",
        };

        var json = JsonConvert.SerializeObject(meta, SerializerSettings);

        json.Should().Contain("\"repository_url\":\"https://github.com/org/repo\"");
        json.Should().Contain("\"correlation_id\":\"corr-12345\"");
    }

    [Fact]
    public void Metadata_Serialize_NullCorrelationIdOmitted()
    {
        var meta = new MetadataTestModel
        {
            RepositoryUrl = "https://github.com/org/repo",
            CorrelationId = null,
        };

        var json = JsonConvert.SerializeObject(meta, SerializerSettings);

        json.Should().Contain("\"repository_url\"");
        // DefaultValueHandling.Ignore omits null (default value for string)
        json.Should().NotContain("\"correlation_id\"");
    }

    // ===== Pattern: SkippableTests deserialization — Data array with SkippableTest attributes =====

    [Fact]
    public void SkippableTests_Deserialize_DataArrayEnvelope()
    {
        // Pattern from TestOptimizationClient.GetSkippableTestsAsync.cs line 65
        // language=json
        var json = """
            {"data":[
                {"id":"1","type":"test","attributes":{"name":"Test1","suite":"Suite1","parameters":null,"configurations":null}},
                {"id":"2","type":"test","attributes":{"name":"Test2","suite":"Suite2","parameters":"{}","configurations":null}}
            ]}
            """;

        var result = JsonConvert.DeserializeObject<DataArrayEnvelopeTestModel<DataTestModel<SkippableTestAttributesTestModel>>>(json);

        result.Data.Should().HaveCount(2);
        result.Data[0].Attributes!.Name.Should().Be("Test1");
        result.Data[0].Attributes!.Suite.Should().Be("Suite1");
        result.Data[1].Attributes!.Name.Should().Be("Test2");
        result.Data[1].Attributes!.Parameters.Should().Be("{}");
    }

    // ===== Test models mirroring TestOptimizationClient internal types =====

    private struct DataEnvelopeTestModel<T>
    {
        [JsonProperty("data")]
        public T Data;

        [JsonProperty("meta")]
        public MetadataTestModel? Meta;
    }

    private struct DataArrayEnvelopeTestModel<T>
    {
        [JsonProperty("data")]
        public T[] Data;

        [JsonProperty("meta")]
        public MetadataTestModel? Meta;
    }

    private struct MetadataTestModel
    {
        [JsonProperty("repository_url")]
        public string RepositoryUrl;

        [JsonProperty("correlation_id")]
        public string? CorrelationId;
    }

    private struct DataTestModel<T>
    {
        [JsonProperty("id")]
        public string? Id;

        [JsonProperty("type")]
        public string Type;

        [JsonProperty("attributes")]
        public T? Attributes;
    }

    private struct SettingsQueryTestModel
    {
        [JsonProperty("service")]
        public string Service;

        [JsonProperty("env")]
        public string Env;

        [JsonProperty("repository_url")]
        public string RepositoryUrl;
    }

    private struct SettingsResponseTestModel
    {
        [JsonProperty("code_coverage")]
        public bool? CodeCoverage;

        [JsonProperty("tests_skipping")]
        public bool? TestsSkipping;

        [JsonProperty("require_git")]
        public bool? RequireGit;

        [JsonProperty("impacted_tests_enabled")]
        public bool? ImpactedTestsEnabled;

        [JsonProperty("flaky_test_retries_enabled")]
        public bool? FlakyTestRetries;

        [JsonProperty("early_flake_detection")]
        public EarlyFlakeDetectionTestModel EarlyFlakeDetection;

        [JsonProperty("known_tests_enabled")]
        public bool? KnownTestsEnabled;

        [JsonProperty("test_management")]
        public TestManagementSettingsTestModel TestManagement;

        [JsonProperty("default_branch")]
        public string? DefaultBranch;

        [JsonProperty("di_enabled")]
        public bool? DynamicInstrumentationEnabled;
    }

    private struct EarlyFlakeDetectionTestModel
    {
        [JsonProperty("enabled")]
        public bool? Enabled;

        [JsonProperty("slow_test_retries")]
        public SlowTestRetriesTestModel SlowTestRetries;

        [JsonProperty("faulty_session_threshold")]
        public int? FaultySessionThreshold;
    }

    private struct SlowTestRetriesTestModel
    {
        [JsonProperty("5s")]
        public int? FiveSeconds;

        [JsonProperty("10s")]
        public int? TenSeconds;

        [JsonProperty("30s")]
        public int? ThirtySeconds;

        [JsonProperty("5m")]
        public int? FiveMinutes;
    }

    private struct TestManagementSettingsTestModel
    {
        [JsonProperty("enabled")]
        public bool? Enabled;

        [JsonProperty("attempt_to_fix_retries")]
        public int? AttemptToFixRetries;
    }

    private class KnownTestsResponseTestModel
    {
        [JsonProperty("tests")]
        public Dictionary<string, Dictionary<string, string[]?>> Tests { get; set; } = null!;
    }

    private class TestManagementResponseTestModel
    {
        [JsonProperty("modules")]
        public Dictionary<string, TestManagementSuitesTestModel> Modules { get; set; } = null!;
    }

    private class TestManagementSuitesTestModel
    {
        [JsonProperty("suites")]
        public Dictionary<string, TestManagementTestsTestModel> Suites { get; set; } = null!;
    }

    private class TestManagementTestsTestModel
    {
        [JsonProperty("tests")]
        public Dictionary<string, TestManagementPropsTestModel> Tests { get; set; } = null!;
    }

    private class TestManagementPropsTestModel
    {
        [JsonProperty("properties")]
        public TestManagementAttrsTestModel Properties { get; set; } = null!;
    }

    private class TestManagementAttrsTestModel
    {
        [JsonProperty("quarantined")]
        public bool Quarantined { get; set; }

        [JsonProperty("disabled")]
        public bool Disabled { get; set; }

        [JsonProperty("attempt_to_fix")]
        public bool AttemptToFix { get; set; }
    }

    private class SkippableTestAttributesTestModel
    {
        [JsonProperty("name")]
        public string Name { get; set; } = null!;

        [JsonProperty("suite")]
        public string Suite { get; set; } = null!;

        [JsonProperty("parameters")]
        public string? Parameters { get; set; }

        [JsonProperty("configurations")]
        public object? Configurations { get; set; }
    }
}
