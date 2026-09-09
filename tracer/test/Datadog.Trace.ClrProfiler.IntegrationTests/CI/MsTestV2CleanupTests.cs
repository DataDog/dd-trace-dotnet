// <copyright file="MsTestV2CleanupTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
[Trait("RunOnWindows", "True")]
public class MsTestV2CleanupTests(ITestOutputHelper output) : TestingFrameworkEvpTest("MSTestTests", output)
{
    public static IEnumerable<object[]> CleanupVersions()
    {
        foreach (var row in PackageVersions.MSTest)
        {
            var packageVersion = (string)row[0];
            // MSTest 4 removed EndOfAssembly; the unversioned sample also uses MSTest 4.
            if (packageVersion.Length > 0 && new Version(packageVersion) < new Version(4, 0))
            {
                yield return [packageVersion, "EndOfAssembly"];
            }

            // Earlier adapters only support cleanup at the end of the assembly.
            if (packageVersion.Length == 0 || new Version(packageVersion) >= new Version(2, 2, 8))
            {
                yield return [packageVersion, "EndOfClass"];
            }
        }
    }

    [Theory]
    [MemberData(nameof(CleanupVersions))]
    public async Task ClassCleanupFailureIsReportedBeforeTheSuiteCloses(string packageVersion, string cleanupLifecycle)
    {
        EnvironmentHelper.EnableDefaultTransport();
        InjectSession(out _, out _, out _, out _, out _, out _, out _);
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "0");
        SetEnvironmentVariable("MSTEST_FAIL_CLASS_CLEANUP", "1");
        SetEnvironmentVariable("TESTINGPLATFORM_TELEMETRY_OPTOUT", "1");
        var logDirectory = Path.Combine(LogDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logDirectory);
        SetEnvironmentVariable(ConfigurationKeys.LogDirectory, logDirectory);
        var suites = new List<JObject>();
        using var agent = EnvironmentHelper.GetMockAgent();
        agent.EventPlatformProxyPayloadReceived += (_, e) =>
        {
            if (e.Value.PathAndQuery.EndsWith("api/v2/libraries/tests/services/setting"))
            {
                e.Value.Response = new MockTracerResponse(GetSettingsJson("false", "false", "false", "0"), 200);
            }
            else if (e.Value.PathAndQuery.EndsWith("api/v2/citestcycle"))
            {
                var payload = JsonConvert.DeserializeObject<MockCIVisibilityProtocol>(e.Value.BodyInJson);
                foreach (var testEvent in payload.Events.Where(testEvent => testEvent.Type == SpanTypes.TestSuite))
                {
                    suites.Add(JObject.Parse(testEvent.Content.ToString()));
                }
            }
        };

        // Before 3.0.1, end-of-assembly cleanup failures are warnings and do not fail the run.
        var warningOnly = packageVersion.Length > 0 && new Version(packageVersion) < new Version(3, 0, 1) && cleanupLifecycle == "EndOfAssembly";
        var arguments = "--TestCaseFilter:FullyQualifiedName=Samples.MSTestTests.TestSuite.SimplePassTest";
#if NETFRAMEWORK
        // The VSTest launcher can be x64 even when this job uses the x86 profiler.
        arguments += " /Platform:" + EnvironmentTools.GetTestTargetPlatform();
#endif
        arguments += $" -- MSTest.ClassCleanupLifecycle={cleanupLifecycle}";
        using var result = await RunDotnetTestSampleAndWaitForExit(
            agent,
            arguments: arguments,
            packageVersion: packageVersion,
            expectedExitCode: warningOnly ? 0 : 1);

        suites.Should().ContainSingle();
        suites.Single()["meta"].Value<string>(TestTags.Status).Should().Be(TestTags.StatusFail);
        suites.Single()["meta"].Value<string>(Tags.ErrorMsg).Should().Contain("MSTest class cleanup failed.");
        var logs = Directory.GetFiles(logDirectory, "dotnet-tracer-managed-*.log");
        logs.Should().NotBeEmpty("the test must inspect the instrumented process logs");
        foreach (var log in logs)
        {
            File.ReadAllText(log).Should().NotContain("SetTag should not be called after the span was closed");
        }
    }
}
