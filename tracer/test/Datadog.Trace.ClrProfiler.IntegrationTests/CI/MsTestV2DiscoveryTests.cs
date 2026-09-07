// <copyright file="MsTestV2DiscoveryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET8_0_OR_GREATER || NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[Trait("Category", "EndToEnd")]
[Trait("Category", "TestIntegrations")]
public class MsTestV2DiscoveryTests(ITestOutputHelper output) : TestingFrameworkEvpTest("MSTestTestsDiscovery", output)
{
#if DEFAULT_SAMPLES
    private const string PackageVersion = "";
#else
    private const string PackageVersion = "4.4.0";
#endif

    [Theory]
#if !DEFAULT_SAMPLES
    [InlineData("4.3.3")]
#endif
    [InlineData(PackageVersion)]
    public Task RepeatingAnInputSourceDoesNotChangeDiscovery(string packageVersion)
        => RunDiscoveryScenario(packageVersion, 20, 14, true, string.Empty, 10, useMtp: false, additionalSample: "MSTestTestsDiscovery");

#if !DEFAULT_SAMPLES
    [Theory]
    [InlineData("4.3.3")]
    [InlineData("4.4.0")]
    public Task MultipleInputAssembliesPreserveTheDiscoveryThreshold(string packageVersion)
        // With this VSTest invocation, 4.3.3 still applies the threshold to the selected sample.
        => RunDiscoveryScenario(packageVersion, 20, 14, true, "FullyQualifiedName~Samples.MSTestTestsDiscovery", 10, useMtp: false, additionalSample: "MSTestTests2");
#endif

    [Theory]
#if !DEFAULT_SAMPLES
    [InlineData("4.3.3", 20, 14, true, "", 10, false)]
#endif
    [InlineData(PackageVersion, 10, 12, true, "", 10, false)]
    [InlineData(PackageVersion, 20, 14, true, "", 10, false)]
    [InlineData(PackageVersion, 30, 16, false, "", 10, false)]
    [InlineData(PackageVersion, 0, 16, false, "", 10, false)]
    [InlineData(PackageVersion, 10, 3, false, "FullyQualifiedName~Case10", 1, false)]
    [InlineData(PackageVersion, 20, 0, false, "FullyQualifiedName~MissingTest", 0, false)]
#if NET8_0_OR_GREATER
    [InlineData(PackageVersion, 10, 12, true, "", 10, true)]
    [InlineData(PackageVersion, 20, 14, true, "", 10, true)]
    [InlineData(PackageVersion, 30, 16, false, "", 10, true)]
    [InlineData(PackageVersion, 0, 16, false, "", 10, true)]
    [InlineData(PackageVersion, 10, 3, false, "FullyQualifiedName~Case10", 1, true)]
    [InlineData(PackageVersion, 20, 0, false, "FullyQualifiedName~MissingTest", 0, true)]
#endif
    public Task DiscoveryControlsTheFaultySessionThreshold(string packageVersion, int threshold, int expectedAttempts, bool faulty, string filter, int expectedTests, bool useMtp)
        => RunDiscoveryScenario(packageVersion, threshold, expectedAttempts, faulty, filter, expectedTests, useMtp);

    private async Task RunDiscoveryScenario(string packageVersion, int threshold, int expectedAttempts, bool faulty, string filter, int expectedTests, bool useMtp, string additionalSample = null)
    {
        EnvironmentHelper.EnableDefaultTransport();
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
        SetEnvironmentVariable("TESTINGPLATFORM_TELEMETRY_OPTOUT", "1");
        // MTP runs directly and can inherit a cache folder from the test host.
        // Give each scenario its own settings without injecting a synthetic session.
        var runId = Guid.NewGuid().ToString("N");
        var cacheFolder = Path.Combine(Path.GetTempPath(), "mstest-discovery-" + runId);
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestOptimizationRunId, runId);
        SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, cacheFolder);
        var tests = new List<MockCIVisibilityTest>();
        var sessions = new List<JObject>();
        using var agent = EnvironmentHelper.GetMockAgent();
        agent.EventPlatformProxyPayloadReceived += (_, e) =>
        {
            if (e.Value.PathAndQuery.EndsWith("api/v2/libraries/tests/services/setting"))
            {
                e.Value.Response = new MockTracerResponse(
                    $$$$"""
                    {"data":{"type":"ci_app_tracers_test_service_settings","attributes":{
                      "code_coverage":false,"itr_enabled":true,"require_git":false,"tests_skipping":false,"known_tests_enabled":true,
                      "early_flake_detection":{"enabled":true,"slow_test_retries":{"5s":3,"10s":3,"30s":3,"5m":3},"faulty_session_threshold":{{{{threshold}}}}}
                    }}}
                    """,
                    200);
            }
            else if (e.Value.PathAndQuery.EndsWith("api/v2/ci/libraries/tests"))
            {
                e.Value.Response = new MockTracerResponse(
                    """
                    {"data":{"type":"ci_app_libraries_tests","attributes":{"tests":{
                      "Samples.MSTestTestsDiscovery":{"Samples.MSTestTestsDiscovery.TestSuite":["Case01","Case02","Case03","Case04","Case05","Case06","Case07"]}
                    }}}}
                    """,
                    200);
            }
            else if (e.Value.PathAndQuery.EndsWith("api/v2/citestcycle"))
            {
                var payload = JsonConvert.DeserializeObject<MockCIVisibilityProtocol>(e.Value.BodyInJson);
                foreach (var testEvent in payload.Events)
                {
                    if (testEvent.Type == SpanTypes.Test)
                    {
                        tests.Add(JsonConvert.DeserializeObject<MockCIVisibilityTest>(testEvent.Content.ToString()));
                    }
                    else if (testEvent.Type == SpanTypes.TestSession)
                    {
                        sessions.Add(JObject.Parse(testEvent.Content.ToString()));
                    }
                }
            }
        };

        try
        {
            var arguments = filter.Length == 0 ? null : (useMtp ? "--filter " : "--TestCaseFilter:") + filter;
            if (additionalSample is not null)
            {
                var additionalEnvironment = new EnvironmentHelper(additionalSample, typeof(MsTestV2DiscoveryTests), Output);
                arguments = "\"" + additionalEnvironment.GetTestCommandForSampleApplicationPath(packageVersion) + "\" " + arguments;
            }

            using var result = await RunDotnetTestSampleAndWaitForExit(agent, arguments: arguments, packageVersion: packageVersion, expectedExitCode: useMtp && expectedTests == 0 ? 8 : 0, useDotnetExec: useMtp);
            tests.Should().HaveCount(expectedAttempts);
            tests.Select(test => test.Meta[TestTags.Name]).Distinct().Should().HaveCount(expectedTests);
            if (expectedTests > 0)
            {
                tests.Should().OnlyContain(test => test.Meta[TestTags.Status] == TestTags.StatusPass);
            }

            if (useMtp && expectedTests == 0)
            {
                // MTP creates the session on the first test, so an empty run creates none.
                sessions.Should().BeEmpty();
                return;
            }

            sessions.Should().ContainSingle();
            sessions.Single().Value<ulong>("duration").Should().BeGreaterThan(0);
            if (faulty)
            {
                sessions.Single()["meta"].Value<string>(EarlyFlakeDetectionTags.AbortReason).Should().Be("faulty");
            }
            else
            {
                sessions.Single()["meta"].Value<string>(EarlyFlakeDetectionTags.AbortReason).Should().BeNull();
            }
        }
        finally
        {
            if (Directory.Exists(cacheFolder))
            {
                Directory.Delete(cacheFolder, recursive: true);
            }
        }
    }
}

#endif
