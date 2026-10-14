// <copyright file="NUnit5EvpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if (NET8_0_OR_GREATER || NETFRAMEWORK) && !DEFAULT_SAMPLES

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[Trait("Area", "CIVisibility")]
public class NUnit5EvpTests : TestingFrameworkEvpTest
{
    public NUnit5EvpTests(ITestOutputHelper output)
        : base("NUnitTests", output)
    {
    }

    public static IEnumerable<object[]> GetData()
    {
        foreach (var version in NUnit5Tests.GetVersions())
        {
            yield return [version[0], "evp_proxy/v2"];
            yield return [version[0], "evp_proxy/v4"];
        }
    }

    [SkippableTheory]
    [MemberData(nameof(GetData))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public async Task ReportsSkippedFixtureAndPreservesPrerequisites(string packageVersion, string endpointToRemove)
    {
        const string SettingsJson = """
                                    {"data":{"type":"ci_app_tracers_test_service_settings","attributes":{"code_coverage":false,"efd_enabled":false,"flaky_test_retries_enabled":false,"itr_enabled":true,"require_git":false,"tests_skipping":true}}}
                                    """;
        const string SkippableTestsJson = """
                                          {"data":[
                                            {"type":"test","attributes":{"name":"PassedPrerequisite","suite":"Samples.NUnit5Tests.DependenciesFixture"}},
                                            {"type":"test","attributes":{"name":"PassedDependent","suite":"Samples.NUnit5Tests.DependenciesFixture"}},
                                            {"type":"test","attributes":{"name":"Independent","suite":"Samples.NUnit5Tests.DependenciesFixture"}}
                                          ],"meta":{"correlation_id":"nunit5-dependencies"}}
                                          """;
        var events = await RunSampleAsync(packageVersion, endpointToRemove, SettingsJson, SkippableTestsJson, testManagementJson: null, "/TestCaseFilter:FullyQualifiedName~Samples.NUnit5Tests -- NUnit.PreFilter=false", expectedExitCode: 1);
        var tests = events.Tests;
        var suites = events.Suites;
        var modules = events.Modules;

        tests.Should().HaveCount(19);
        suites.Should().HaveCount(4);
        modules.Should().ContainSingle();
        tests.Select(t => t.SpanId).Should().OnlyHaveUniqueItems();
        var skippedSuite = suites.Single(s => s.Meta[TestTags.Suite] == "Samples.NUnit5Tests.DependentFixture");
        skippedSuite.Meta[TestTags.Status].Should().Be(TestTags.StatusSkip);
        var skippedCases = tests.Where(t => t.Meta[TestTags.Suite] == "Samples.NUnit5Tests.DependentFixture").ToArray();
        skippedCases.Should().HaveCount(3);
        skippedCases.Should().OnlyContain(t => t.TestSuiteId == skippedSuite.TestSuiteId && t.Meta[TestTags.Status] == TestTags.StatusSkip);
        skippedCases.Should().OnlyContain(t => t.Meta[TestTags.SkipReason].StartsWith("Dependency on "));

        foreach (var name in new[] { "PassedPrerequisite", "PassedDependent" })
        {
            tests.Single(t => t.Meta[TestTags.Name] == name).Meta[TestTags.Status].Should().Be(TestTags.StatusPass);
        }

        var independent = tests.Single(t => t.Meta[TestTags.Name] == "Independent");
        independent.Meta[TestTags.Status].Should().Be(TestTags.StatusSkip);
        independent.Meta[IntelligentTestRunnerTags.SkippedBy].Should().Be("true");
        modules.Single().Metrics[IntelligentTestRunnerTags.SkippingCount].Should().Be(1);
    }

    [SkippableTheory]
    [MemberData(nameof(GetData))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "QuarantinedTests")]
    public async Task QuarantineHidesOnlyFailuresFromNUnitDependencies(string packageVersion, string endpointToRemove)
    {
        const string SettingsJson = """
                                    {"data":{"type":"ci_app_tracers_test_service_settings","attributes":{"code_coverage":false,"efd_enabled":false,"flaky_test_retries_enabled":false,"itr_enabled":false,"require_git":false,"tests_skipping":false,"test_management":{"enabled":true,"attempt_to_fix_retries":0}}}}
                                    """;
        const string TestManagementJson = """
                                          {"data":{"id":"nunit5-quarantine","type":"ci_app_libraries_tests","attributes":{"modules":{"Samples.NUnitTests":{"suites":{
                                            "Samples.NUnit5Tests.DependenciesFixture":{"tests":{"PassedPrerequisite":{"properties":{"quarantined":true}}}},
                                            "Samples.NUnit5Tests.FailingFixture":{"tests":{"WarningBeforeFailure":{"properties":{"quarantined":true}},"Failed":{"properties":{"quarantined":true}}}}
                                          }}}}}}
                                          """;
        // With parallel workers NUnit evaluates a fixture dependency before the queued prerequisite fixture completes.
        // Running without workers makes NUnit decide from the completed prerequisite fixture result.
        const string Arguments = "/TestCaseFilter:FullyQualifiedName=Samples.NUnit5Tests.DependenciesFixture.PassedDependent|FullyQualifiedName~Samples.NUnit5Tests.DependentFixture.SkippedFixtureCase -- NUnit.PreFilter=false NUnit.NumberOfTestWorkers=0";

        // The adapter receives no individual results from the blocked fixture or from quarantined prerequisites, so it reports exit code zero.
        var events = await RunSampleAsync(packageVersion, endpointToRemove, SettingsJson, skippableTestsJson: null, TestManagementJson, Arguments, expectedExitCode: 0);
        var tests = events.Tests;

        tests.Should().HaveCount(6);
        foreach (var name in new[] { "PassedPrerequisite", "WarningBeforeFailure", "Failed" })
        {
            tests.Single(t => t.Meta[TestTags.Name] == name).Meta[TestTags.TestIsQuarantined].Should().Be("true");
        }

        tests.Single(t => t.Meta[TestTags.Name] == "PassedPrerequisite").Meta[TestTags.Status].Should().Be(TestTags.StatusPass);
        tests.Single(t => t.Meta[TestTags.Name] == "PassedDependent").Meta[TestTags.Status].Should().Be(TestTags.StatusPass);

        // NUnit keeps the warning, but the hidden failure after it must still block the dependents of its fixture.
        tests.Single(t => t.Meta[TestTags.Name] == "WarningBeforeFailure").Meta[TestTags.Status].Should().Be(TestTags.StatusPass);
        tests.Single(t => t.Meta[TestTags.Name] == "Failed").Meta[TestTags.Status].Should().Be(TestTags.StatusFail);
        var skippedCases = tests.Where(t => t.Meta[TestTags.Name] == "SkippedFixtureCase").ToArray();
        skippedCases.Should().HaveCount(2);
        skippedCases.Should().OnlyContain(t => t.Meta[TestTags.Status] == TestTags.StatusSkip && t.Meta[TestTags.SkipReason].StartsWith("Dependency on "));
    }

    private async Task<CapturedEvents> RunSampleAsync(string packageVersion, string endpointToRemove, string settingsJson, string skippableTestsJson, string testManagementJson, string arguments, int expectedExitCode)
    {
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
        InjectSession(out _, out _, out _, out _, out _, out _, out _);
        using var agent = EnvironmentHelper.GetMockAgent();
        agent.Configuration.Endpoints = agent.Configuration.Endpoints.Where(e => !e.Contains(endpointToRemove)).ToArray();
        var events = new CapturedEvents();
        agent.EventPlatformProxyPayloadReceived += (_, e) =>
        {
            if (e.Value.PathAndQuery.EndsWith("api/v2/libraries/tests/services/setting"))
            {
                e.Value.Response = new MockTracerResponse(settingsJson, 200);
            }
            else if (e.Value.PathAndQuery.EndsWith("api/v2/ci/tests/skippable") && skippableTestsJson is not null)
            {
                e.Value.Response = new MockTracerResponse(skippableTestsJson, 200);
            }
            else if (e.Value.PathAndQuery.EndsWith("api/v2/test/libraries/test-management/tests") && testManagementJson is not null)
            {
                e.Value.Response = new MockTracerResponse(testManagementJson, 200);
            }
            else if (e.Value.PathAndQuery.EndsWith("api/v2/citestcycle"))
            {
                var payload = JsonConvert.DeserializeObject<MockCIVisibilityProtocol>(e.Value.BodyInJson);
                foreach (var item in payload.Events)
                {
                    var json = item.Content.ToString();
                    if (item.Type == SpanTypes.Test)
                    {
                        events.Tests.Add(JsonConvert.DeserializeObject<MockCIVisibilityTest>(json));
                    }
                    else if (item.Type == SpanTypes.TestSuite)
                    {
                        events.Suites.Add(JsonConvert.DeserializeObject<MockCIVisibilityTestSuite>(json));
                    }
                    else if (item.Type == SpanTypes.TestModule)
                    {
                        events.Modules.Add(JsonConvert.DeserializeObject<MockCIVisibilityTestModule>(json));
                    }
                }
            }
        };

        using var process = await RunDotnetTestSampleAndWaitForExit(agent, arguments: arguments, packageVersion: packageVersion, expectedExitCode: expectedExitCode);
        return events;
    }

    private sealed class CapturedEvents
    {
        public List<MockCIVisibilityTest> Tests { get; } = [];

        public List<MockCIVisibilityTestSuite> Suites { get; } = [];

        public List<MockCIVisibilityTestModule> Modules { get; } = [];
    }
}

#endif
