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
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
        InjectSession(out _, out _, out _, out _, out _, out _, out _);
        using var agent = EnvironmentHelper.GetMockAgent();
        agent.Configuration.Endpoints = agent.Configuration.Endpoints.Where(e => !e.Contains(endpointToRemove)).ToArray();
        var tests = new List<MockCIVisibilityTest>();
        var suites = new List<MockCIVisibilityTestSuite>();
        var modules = new List<MockCIVisibilityTestModule>();
        agent.EventPlatformProxyPayloadReceived += (_, e) =>
        {
            if (e.Value.PathAndQuery.EndsWith("api/v2/libraries/tests/services/setting"))
            {
                e.Value.Response = new MockTracerResponse(
                    """
                    {"data":{"type":"ci_app_tracers_test_service_settings","attributes":{"code_coverage":false,"efd_enabled":false,"flaky_test_retries_enabled":false,"itr_enabled":true,"require_git":false,"tests_skipping":true}}}
                    """,
                    200);
            }
            else if (e.Value.PathAndQuery.EndsWith("api/v2/ci/tests/skippable"))
            {
                e.Value.Response = new MockTracerResponse(
                    """
                    {"data":[
                      {"type":"test","attributes":{"name":"PassedPrerequisite","suite":"Samples.NUnit5Tests.DependenciesFixture"}},
                      {"type":"test","attributes":{"name":"PassedDependent","suite":"Samples.NUnit5Tests.DependenciesFixture"}},
                      {"type":"test","attributes":{"name":"Independent","suite":"Samples.NUnit5Tests.DependenciesFixture"}}
                    ],"meta":{"correlation_id":"nunit5-dependencies"}}
                    """,
                    200);
            }
            else if (e.Value.PathAndQuery.EndsWith("api/v2/citestcycle"))
            {
                var payload = JsonConvert.DeserializeObject<MockCIVisibilityProtocol>(e.Value.BodyInJson);
                foreach (var item in payload.Events)
                {
                    var json = item.Content.ToString();
                    if (item.Type == SpanTypes.Test)
                    {
                        tests.Add(JsonConvert.DeserializeObject<MockCIVisibilityTest>(json));
                    }
                    else if (item.Type == SpanTypes.TestSuite)
                    {
                        suites.Add(JsonConvert.DeserializeObject<MockCIVisibilityTestSuite>(json));
                    }
                    else if (item.Type == SpanTypes.TestModule)
                    {
                        modules.Add(JsonConvert.DeserializeObject<MockCIVisibilityTestModule>(json));
                    }
                }
            }
        };

        using var process = await RunDotnetTestSampleAndWaitForExit(agent, arguments: "/TestCaseFilter:FullyQualifiedName~Samples.NUnit5Tests -- NUnit.PreFilter=false", packageVersion: packageVersion, expectedExitCode: 1);

        tests.Should().HaveCount(18);
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
}

#endif
