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

[Trait("Area", "CIVisibility")]
[Trait("Category", "EndToEnd")]
[Trait("Category", "TestIntegrations")]
[Trait("RunOnWindows", "True")]
public class MsTestV2DiscoveryTests(ITestOutputHelper output) : TestingFrameworkEvpTest("MSTestTests", output)
{
    // Run three existing passing tests. Only SimplePassTest is known; the other two exercise EFD.
    // Discovery must still count the entire assembly, including the tests excluded by this filter.
    private const string PassingTestsFilter = "FullyQualifiedName=Samples.MSTestTests.TestSuite.SimplePassTest|" +
                                              "FullyQualifiedName=Samples.MSTestTests.TestSuite.TraitPassTest|" +
                                              "FullyQualifiedName=Samples.MSTestTests.TestSuite.UnskippableTest";

    public static IEnumerable<object[]> InputSources()
    {
        foreach (var version in PackageVersions.MSTest)
        {
            var packageVersion = (string)version[0];
            if (packageVersion.Length > 0 && new Version(packageVersion) < new Version(4, 3, 3))
            {
                continue;
            }

            yield return [packageVersion, "MSTestTests"];
            // The second assembly is available at matching versions only in the versioned matrix.
            if (packageVersion.Length > 0 && PackageVersions.MSTest2.Any(other => (string)other[0] == packageVersion))
            {
                yield return [packageVersion, "MSTestTests2"];
            }
        }
    }

    public static IEnumerable<object[]> DiscoveryScenarios()
    {
        // There are 21 discovered cases and two new tests among the three we run. Thresholds of
        // 1%, 5%, and 10% allow zero, one, or both new tests to receive their two extra attempts.
        foreach (var version in PackageVersions.MSTest)
        {
            var packageVersion = (string)version[0];
            if (packageVersion.Length > 0 && new Version(packageVersion) < new Version(4, 3, 3))
            {
                continue;
            }

            yield return [packageVersion, 5, 5, true, PassingTestsFilter, 3, false];
            // Keep the pre-4.4 discovery regression; the remaining cases exercise the new async hook.
            if (packageVersion.Length > 0 && new Version(packageVersion) < new Version(4, 4))
            {
                continue;
            }

            yield return [packageVersion, 1, 3, true, PassingTestsFilter, 3, false];
            yield return [packageVersion, 10, 7, false, PassingTestsFilter, 3, false];
            yield return [packageVersion, 0, 7, false, PassingTestsFilter, 3, false];
            yield return [packageVersion, 5, 3, false, "FullyQualifiedName=Samples.MSTestTests.TestSuite.UnskippableTest", 1, false];
            yield return [packageVersion, 20, 0, false, "FullyQualifiedName~MissingTest", 0, false];
#if NET8_0_OR_GREATER
            yield return [packageVersion, 1, 3, true, PassingTestsFilter, 3, true];
            yield return [packageVersion, 5, 5, true, PassingTestsFilter, 3, true];
            yield return [packageVersion, 10, 7, false, PassingTestsFilter, 3, true];
            yield return [packageVersion, 0, 7, false, PassingTestsFilter, 3, true];
            yield return [packageVersion, 5, 3, false, "FullyQualifiedName=Samples.MSTestTests.TestSuite.UnskippableTest", 1, true];
            yield return [packageVersion, 20, 0, false, "FullyQualifiedName~MissingTest", 0, true];
#endif
        }
    }

    [Theory]
    [MemberData(nameof(InputSources))]
    public Task InputSourcesPreserveTheDiscoveryThreshold(string packageVersion, string additionalSample)
    {
        // VSTest deduplicates a repeated source. With two assemblies, each executes three tests
        // and retries one of them: both its 21-case and 20-case discovery totals allow one at 5%.
        var repeatedSource = additionalSample == "MSTestTests";
        return RunDiscoveryScenario(packageVersion, 5, repeatedSource ? 5 : 10, true, PassingTestsFilter, repeatedSource ? 3 : 6, useMtp: false, additionalSample: additionalSample);
    }

    [Theory]
    [MemberData(nameof(DiscoveryScenarios))]
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
                      "Samples.MSTestTests":{"Samples.MSTestTests.TestSuite":["SimplePassTest"]},
                      "Samples.MSTestTests2":{"Samples.MSTestTests.TestSuite":["SimplePassTest"]}
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

#if NETFRAMEWORK
            arguments += " /Platform:" + EnvironmentTools.GetTestTargetPlatform();
#endif
            using var result = await RunDotnetTestSampleAndWaitForExit(agent, arguments: arguments, packageVersion: packageVersion, expectedExitCode: useMtp && expectedTests == 0 ? 8 : 0, useDotnetExec: useMtp);
            tests.Should().HaveCount(expectedAttempts);
            tests.GroupBy(test => test.Meta[TestTags.Bundle])
                 .Sum(module => module.Select(test => test.Meta[TestTags.Name]).Distinct().Count())
                 .Should().Be(expectedTests);
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
