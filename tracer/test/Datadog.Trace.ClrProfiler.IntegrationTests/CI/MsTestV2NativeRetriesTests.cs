// <copyright file="MsTestV2NativeRetriesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET8_0_OR_GREATER || NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Globalization;
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
public class MsTestV2NativeRetriesTests : TestingFrameworkEvpTest
{
#if DEFAULT_SAMPLES
    private const string PackageVersion = "";
#else
    private const string PackageVersion = "4.4.0";
#endif

    public MsTestV2NativeRetriesTests(ITestOutputHelper output)
        : this("MSTestTestsNativeRetries", output)
    {
    }

    protected MsTestV2NativeRetriesTests(string sample, ITestOutputHelper output)
        : base(sample, output)
    {
    }

    protected virtual bool UseMtp => false;

    [Fact]
    public async Task CleanupFailureAfterNativeRetryBelongsToTheSuite()
    {
        EnvironmentHelper.EnableDefaultTransport();
        InjectSession(out _, out _, out _, out _, out _, out _, out _);
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "0");
        SetEnvironmentVariable("TESTINGPLATFORM_TELEMETRY_OPTOUT", "1");
        var tests = new List<MockCIVisibilityTest>();
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
                foreach (var testEvent in payload.Events)
                {
                    if (testEvent.Type == SpanTypes.Test)
                    {
                        tests.Add(JsonConvert.DeserializeObject<MockCIVisibilityTest>(testEvent.Content.ToString()));
                    }
                    else if (testEvent.Type == SpanTypes.TestSuite)
                    {
                        suites.Add(JObject.Parse(testEvent.Content.ToString()));
                    }
                }
            }
        };

        var filter = UseMtp ? "--filter FullyQualifiedName~PassesBeforeCleanupFailure" : "--TestCaseFilter:FullyQualifiedName~PassesBeforeCleanupFailure";
        using var result = await RunDotnetTestSampleAndWaitForExit(agent, arguments: filter, packageVersion: PackageVersion, expectedExitCode: UseMtp ? 2 : 1, useDotnetExec: UseMtp);
        tests.Should().HaveCount(2);
        tests.Single(test => test.Meta.ContainsKey(TestTags.TestFinalStatus)).Meta[TestTags.TestFinalStatus].Should().Be(TestTags.StatusPass);
        suites.Should().ContainSingle();
        suites.Single()["meta"].Value<string>(TestTags.Status).Should().Be(TestTags.StatusFail);
        suites.Single()["meta"].Value<string>(Tags.ErrorMsg).Should().Contain("Class cleanup failed after the retry.");
    }

    [Theory]
    [InlineData("MixedRowOutcomes", 4, 2, 0, 0, false)]
    [InlineData("TimesOut", 3, 1, 1, 2, false)]
    [InlineData("RegressesAfterPassing", 6, 2, 1, 2, false)]
    [InlineData("RegressesAfterPassing", 7, 2, 0, 0, true)]
    [InlineData("RetriesUseFreshInstances", 4, 1, 0, 0, true)]
    [InlineData("CustomPolicyContinuesAfterPassing", 3, 1, 1, 2, false)]
    [InlineData("CustomPolicyContinuesAfterPassing", 4, 1, 0, 0, true)]
    [InlineData("DelegatingExecutor", 2, 1, 0, 0, false)]
    [InlineData("MethodRetryOverridesClass", 2, 1, 1, 2, false)]
    [InlineData("MultipleResults", 6, 2, 1, 2, false)]
    [InlineData("MultipleResults", 7, 2, 0, 0, true)]
    public async Task NativeRetryPolicyHandlesMixedRowsAndTimeouts(string name, int expectedAttempts, int expectedRows, int vstestExitCode, int mtpExitCode, bool automaticRetries)
    {
        EnvironmentHelper.EnableDefaultTransport();
        InjectSession(out _, out _, out _, out _, out _, out _, out _);
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.FlakyRetryEnabled, automaticRetries ? "1" : "0");
        SetEnvironmentVariable("TESTINGPLATFORM_TELEMETRY_OPTOUT", "1");
        var attemptsFile = Path.GetTempFileName();
        var historyFile = Path.GetTempFileName();
        SetEnvironmentVariable("MSTEST_ATTEMPTS_FILE", attemptsFile);
        SetEnvironmentVariable("MSTEST_RETRY_HISTORY_FILE", historyFile);
        var tests = new List<MockCIVisibilityTest>();
        using var agent = EnvironmentHelper.GetMockAgent();
        agent.EventPlatformProxyPayloadReceived += (_, e) =>
        {
            if (e.Value.PathAndQuery.EndsWith("api/v2/libraries/tests/services/setting"))
            {
                e.Value.Response = new MockTracerResponse(GetSettingsJson("false", "false", "false", "0", automaticRetries ? "true" : "false"), 200);
            }
            else if (e.Value.PathAndQuery.EndsWith("api/v2/citestcycle"))
            {
                var payload = JsonConvert.DeserializeObject<MockCIVisibilityProtocol>(e.Value.BodyInJson);
                foreach (var testEvent in payload.Events.Where(testEvent => testEvent.Type == SpanTypes.Test))
                {
                    tests.Add(JsonConvert.DeserializeObject<MockCIVisibilityTest>(testEvent.Content.ToString()));
                }
            }
        };

        try
        {
            var filter = UseMtp ? $"--filter FullyQualifiedName~{name}" : $"--TestCaseFilter:FullyQualifiedName~{name}";
            using var result = await RunDotnetTestSampleAndWaitForExit(agent, arguments: filter, packageVersion: PackageVersion, expectedExitCode: UseMtp ? mtpExitCode : vstestExitCode, useDotnetExec: UseMtp);
            File.ReadAllLines(attemptsFile).Should().HaveCount(expectedAttempts);
            tests.Should().HaveCount(expectedAttempts);
            tests.Count(test => test.Meta.ContainsKey(TestTags.TestFinalStatus)).Should().Be(expectedRows);
            tests.Count(test => test.Meta.ContainsKey(TestTags.TestIsRetry)).Should().Be(expectedAttempts - expectedRows);
            if (name == "MixedRowOutcomes")
            {
                tests.Where(test => test.Meta.ContainsKey(TestTags.TestFinalStatus)).Select(test => test.Meta[TestTags.TestFinalStatus]).Should().BeEquivalentTo([TestTags.StatusSkip, TestTags.StatusPass]);
            }
            else if (name == "MultipleResults")
            {
                tests.Where(test => test.Meta.ContainsKey(TestTags.TestFinalStatus)).Select(test => test.Meta[TestTags.TestFinalStatus]).Should().BeEquivalentTo([automaticRetries ? TestTags.StatusPass : TestTags.StatusFail, TestTags.StatusPass]);
                tests.Select(test => test.Meta[TestTags.Name]).Distinct().Should().BeEquivalentTo("First result", "Second result");
                if (UseMtp)
                {
                    File.ReadAllLines(historyFile).Should().BeEquivalentTo(
                        "First result|1|True",
                        "Second result|1|True",
                        "First result|2|True",
                        "Second result|2|True",
                        "First result|3|False",
                        "Second result|3|False");
                }
            }
            else if (name == "RegressesAfterPassing")
            {
                if (UseMtp)
                {
                    File.ReadAllLines(historyFile).Should().BeEquivalentTo(
                        "RegressesAfterPassing (0)|1|True",
                        "RegressesAfterPassing (1)|1|True",
                        "RegressesAfterPassing (0)|2|True",
                        "RegressesAfterPassing (1)|2|True",
                        "RegressesAfterPassing (0)|3|False",
                        "RegressesAfterPassing (1)|3|False");
                }

                var rows = tests.GroupBy(test => test.Meta[TestTags.Parameters]).ToArray();
                rows.Should().HaveCount(2);
                foreach (var row in rows)
                {
                    var attempts = row.OrderBy(test => test.Start).ToArray();
                    attempts.Length.Should().BeInRange(3, automaticRetries ? 4 : 3);
                    attempts.Take(attempts.Length - 1).Should().OnlyContain(test => !test.Meta.ContainsKey(TestTags.TestFinalStatus));
                    attempts.Last().Meta[TestTags.TestFinalStatus].Should().Be(attempts.Last().Meta[TestTags.Status]);
                }

                tests.Where(test => test.Meta.ContainsKey(TestTags.TestFinalStatus)).Select(test => test.Meta[TestTags.TestFinalStatus]).Should().BeEquivalentTo([automaticRetries ? TestTags.StatusPass : TestTags.StatusFail, TestTags.StatusPass]);
            }
            else if (name is "RetriesUseFreshInstances" or "CustomPolicyContinuesAfterPassing" or "DelegatingExecutor")
            {
                tests.OrderBy(test => test.Start).Last().Meta[TestTags.TestFinalStatus].Should().Be(vstestExitCode == 0 ? TestTags.StatusPass : TestTags.StatusFail);
            }
            else
            {
                tests.Should().OnlyContain(test => test.Meta[TestTags.Status] == TestTags.StatusFail);
            }
        }
        finally
        {
            Output.WriteLine(JsonConvert.SerializeObject(tests, Formatting.Indented));
            File.Delete(attemptsFile);
            File.Delete(historyFile);
        }
    }

    [Theory]
    [InlineData("EmptyFinalRetry", 0, 8, 1)]
    [InlineData("ThrowingRetry", 1, 2, 1)]
    [InlineData("CanceledRetry", 1, 2, 1)]
    [InlineData("AsyncThrowingRetry", 1, 2, 1)]
    [InlineData("AsyncCanceledRetry", 1, 2, 1)]
    [InlineData("EmptyExecutor", 1, 2, 1)]
    [InlineData("ThrowingExecutor", 1, 2, 3)]
    [InlineData("AssemblyInitializationFailure", 1, 2, 1)]
    [InlineData("ClassInitializationFailure", 1, 2, 1)]
    public async Task FrameworkErrorsPreserveRunnerBehavior(string name, int vstestExitCode, int mtpExitCode, int expectedAttempts)
    {
        EnvironmentHelper.EnableDefaultTransport();
        InjectSession(out _, out _, out _, out _, out _, out _, out _);
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "0");
        SetEnvironmentVariable("TESTINGPLATFORM_TELEMETRY_OPTOUT", "1");
        if (name == "AssemblyInitializationFailure")
        {
            SetEnvironmentVariable("MSTEST_FAIL_ASSEMBLY_INITIALIZE", "1");
        }
        else if (name == "ClassInitializationFailure")
        {
            SetEnvironmentVariable("MSTEST_FAIL_CLASS_INITIALIZE", "1");
        }

        var tests = new List<MockCIVisibilityTest>();
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
                foreach (var testEvent in payload.Events.Where(testEvent => testEvent.Type == SpanTypes.Test))
                {
                    tests.Add(JsonConvert.DeserializeObject<MockCIVisibilityTest>(testEvent.Content.ToString()));
                }
            }
        };

        var testName = name is "AssemblyInitializationFailure" or "ClassInitializationFailure" ? "PassesAfterClassInitialization" : name;
        var filter = UseMtp ? $"--filter FullyQualifiedName~.{testName}" : $"--TestCaseFilter:FullyQualifiedName~.{testName}";
        using var result = await RunDotnetTestSampleAndWaitForExit(agent, arguments: filter, packageVersion: PackageVersion, expectedExitCode: UseMtp ? mtpExitCode : vstestExitCode, useDotnetExec: UseMtp);
        tests.Should().HaveCount(expectedAttempts);
        tests.Should().OnlyContain(test => test.Meta[TestTags.Status] == TestTags.StatusFail);
        tests.Count(test => test.Meta.ContainsKey(TestTags.TestIsRetry)).Should().Be(expectedAttempts - 1);
        tests.Count(test => test.Meta.ContainsKey(TestTags.TestFinalStatus)).Should().Be(1);
        tests.Single(test => test.Meta.ContainsKey(TestTags.TestFinalStatus)).Meta[TestTags.TestFinalStatus].Should().Be(TestTags.StatusFail);
        if (name == "ThrowingExecutor")
        {
            tests.Should().OnlyContain(test => test.Error == 1 && test.Meta[Tags.ErrorMsg] == "Custom executor failed.");
        }
    }

    [Theory]
    [InlineData("efd", "AlwaysFails", 5, "fail", 3)]
    [InlineData("efd_faulty", "AlwaysFails", 3, "fail", 3)]
    [InlineData("efd_and_atr", "AlwaysFails", 5, "fail", 3)]
    [InlineData("attempt_to_fix", "AlwaysFails", 5, "fail", 3)]
    [InlineData("attempt_to_fix", "PassesImmediately", 3, "pass", 1)]
    [InlineData("attempt_to_fix", "PassesOnThirdAttempt", 5, "fail", 3)]
    [InlineData("quarantined", "AlwaysFails", 3, "skip", 3)]
    [InlineData("disabled", "AlwaysFails", 0, "skip", 3)]
    [InlineData("itr", "AlwaysFails", 0, "skip", 3)]
    [InlineData("itr_row", "ParameterizedRetry", 2, "pass", 2)]
    [InlineData("efd", "InitiallyPassesWithFreshInstances", 3, "pass", 1)]
    public async Task NativeRetriesRespectTestOptimizationPolicies(string feature, string name, int expectedAttempts, string expectedFinalStatus, int expectedNativeAttempts)
    {
        EnvironmentHelper.EnableDefaultTransport();
        InjectSession(out _, out _, out _, out _, out _, out _, out _);
        var isEfd = feature.StartsWith("efd", StringComparison.Ordinal);
        var isAtr = feature == "efd_and_atr";
        var isItr = feature is "itr" or "itr_row";
        var skipOneRow = feature == "itr_row";
        var module = UseMtp ? "Samples.MSTestTestsNativeRetriesMtp" : "Samples.MSTestTestsNativeRetries";
        var attemptsFile = Path.GetTempFileName();
        SetEnvironmentVariable("MSTEST_ATTEMPTS_FILE", attemptsFile);
        SetEnvironmentVariable("TESTINGPLATFORM_TELEMETRY_OPTOUT", "1");
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.FlakyRetryEnabled, isAtr ? "1" : "0");
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.FlakyRetryCount, "2");
        var tests = new List<MockCIVisibilityTest>();
        using var agent = EnvironmentHelper.GetMockAgent();
        agent.EventPlatformProxyPayloadReceived += (_, e) =>
        {
            if (e.Value.PathAndQuery.EndsWith("api/v2/libraries/tests/services/setting"))
            {
                var settings = GetSettingsJson(isEfd ? "true" : "false", isItr ? "true" : "false", isEfd || isItr ? "false" : "true", "3", isAtr ? "true" : "false")
                              .Replace("\"faulty_session_threshold\": 100", feature == "efd_faulty" ? "\"faulty_session_threshold\": 1" : "\"faulty_session_threshold\": 0")
                              .Replace(": 10", ": 3");
                e.Value.Response = new MockTracerResponse(settings, 200);
            }
            else if (e.Value.PathAndQuery.EndsWith("api/v2/ci/libraries/tests"))
            {
                e.Value.Response = new MockTracerResponse("""{"data":{"type":"ci_app_libraries_tests","attributes":{"tests":{}}}}""", 200);
            }
            else if (e.Value.PathAndQuery.EndsWith("api/v2/ci/tests/skippable"))
            {
                e.Value.Response = new MockTracerResponse(
                    JsonConvert.SerializeObject(new
                    {
                        data = new[]
                        {
                            new
                            {
                                id = name,
                                type = skipOneRow ? "test_params" : "test",
                                attributes = new
                                {
                                    suite = "Samples.MSTestTestsNativeRetries.TestSuite",
                                    name,
                                    parameters = skipOneRow ? """{"metadata":{},"arguments":{"row":"0"}}""" : null,
                                    _missing_line_code_coverage = false
                                }
                            }
                        }
                    }),
                    200);
            }
            else if (e.Value.PathAndQuery.EndsWith("api/v2/test/libraries/test-management/tests"))
            {
                var response = new
                {
                    data = new
                    {
                        type = "ci_app_libraries_tests",
                        attributes = new
                        {
                            modules = new Dictionary<string, object>
                            {
                                [module] = new
                                {
                                    suites = new Dictionary<string, object>
                                    {
                                        ["Samples.MSTestTestsNativeRetries.TestSuite"] = new
                                        {
                                            tests = new Dictionary<string, object>
                                            {
                                                [name] = new { properties = new Dictionary<string, bool> { [feature] = true } }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                e.Value.Response = new MockTracerResponse(JsonConvert.SerializeObject(response), 200);
            }
            else if (e.Value.PathAndQuery.EndsWith("api/v2/citestcycle"))
            {
                var payload = JsonConvert.DeserializeObject<MockCIVisibilityProtocol>(e.Value.BodyInJson);
                foreach (var testEvent in payload.Events.Where(testEvent => testEvent.Type == SpanTypes.Test))
                {
                    tests.Add(JsonConvert.DeserializeObject<MockCIVisibilityTest>(testEvent.Content.ToString()));
                }
            }
        };

        try
        {
            var filter = UseMtp ? $"--filter FullyQualifiedName~{name}" : $"--TestCaseFilter:FullyQualifiedName~{name}";
            using var result = await RunDotnetTestSampleAndWaitForExit(agent, arguments: filter, packageVersion: PackageVersion, expectedExitCode: isEfd && expectedFinalStatus == TestTags.StatusFail ? (UseMtp ? 2 : 1) : 0, useDotnetExec: UseMtp);
            File.ReadAllLines(attemptsFile).Should().HaveCount(expectedAttempts);
            if (skipOneRow)
            {
                tests.Should().HaveCount(expectedAttempts + 1);
                File.ReadAllLines(attemptsFile).Should().OnlyContain(line => line.StartsWith("ParameterizedRetry1:", StringComparison.Ordinal));
                var skipped = tests.Single(test => test.Meta[TestTags.Status] == TestTags.StatusSkip);
                skipped.Meta.Should().NotContainKey(TestTags.TestIsRetry);
                tests.Where(test => test.Meta.ContainsKey(TestTags.TestFinalStatus)).Select(test => test.Meta[TestTags.TestFinalStatus]).Should().BeEquivalentTo(TestTags.StatusSkip, TestTags.StatusPass);
                return;
            }

            tests.Should().HaveCount(Math.Max(1, expectedAttempts));
            tests.Count(test => test.Meta.ContainsKey(TestTags.TestFinalStatus)).Should().Be(1);
            tests.Single(test => test.Meta.ContainsKey(TestTags.TestFinalStatus)).Meta[TestTags.TestFinalStatus].Should().Be(expectedFinalStatus);
            if (feature == "attempt_to_fix")
            {
                tests.Single(test => test.Meta.ContainsKey(TestTags.TestFinalStatus)).Meta[TestTags.TestAttemptToFixPassed].Should().Be(expectedFinalStatus == TestTags.StatusPass ? "true" : "false");
            }

            if (isEfd || feature == "attempt_to_fix")
            {
                var retryReason = isEfd ? TestTags.TestRetryReasonEfd : TestTags.TestRetryReasonAttemptToFix;
                tests.Count(test => test.Meta.TryGetValue(TestTags.TestRetryReason, out var reason) && reason == retryReason).Should().Be(expectedAttempts - expectedNativeAttempts);
                tests.Should().NotContain(test => test.Meta.ContainsKey(TestTags.TestRetryReason) && test.Meta[TestTags.TestRetryReason] == TestTags.TestRetryReasonAtr);
            }
        }
        catch
        {
            Output.WriteLine(JsonConvert.SerializeObject(tests, Formatting.Indented));
            throw;
        }
        finally
        {
            File.Delete(attemptsFile);
        }
    }

    [Theory]
    [InlineData(PackageVersion, false, 1000)]
    [InlineData(PackageVersion, true, 1000)]
    [InlineData(PackageVersion, true, 1)]
    public async Task NativeRetriesCompleteBeforeAutomaticRetries(string packageVersion, bool automaticRetries, int totalRetryCount)
    {
        EnvironmentHelper.EnableDefaultTransport();
        InjectSession(out _, out _, out _, out _, out _, out _, out _);
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.FlakyRetryEnabled, automaticRetries ? "1" : "0");
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.FlakyRetryCount, "2");
        var attemptsFile = Path.GetTempFileName();
        var historyFile = Path.GetTempFileName();
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TotalFlakyRetryCount, totalRetryCount.ToString(CultureInfo.InvariantCulture));
        SetEnvironmentVariable("MSTEST_RETRY_HISTORY_FILE", historyFile);
        SetEnvironmentVariable("TESTINGPLATFORM_TELEMETRY_OPTOUT", "1");
        SetEnvironmentVariable("MSTEST_ATTEMPTS_FILE", attemptsFile);
        var tests = new List<MockCIVisibilityTest>();

        using var agent = EnvironmentHelper.GetMockAgent();
        agent.EventPlatformProxyPayloadReceived += (_, e) =>
        {
            if (e.Value.PathAndQuery.EndsWith("api/v2/libraries/tests/services/setting"))
            {
                e.Value.Response = new MockTracerResponse(GetSettingsJson("false", "false", "false", "0", automaticRetries ? "true" : "false"), 200);
            }
            else if (e.Value.PathAndQuery.EndsWith("api/v2/citestcycle"))
            {
                var payload = JsonConvert.DeserializeObject<MockCIVisibilityProtocol>(e.Value.BodyInJson);
                foreach (var testEvent in payload.Events.Where(testEvent => testEvent.Type == SpanTypes.Test))
                {
                    tests.Add(JsonConvert.DeserializeObject<MockCIVisibilityTest>(testEvent.Content.ToString()));
                }
            }
        };

        try
        {
            using var result = await RunDotnetTestSampleAndWaitForExit(agent, arguments: UseMtp ? "--filter TestCategory!=CustomRetry" : "--TestCaseFilter:TestCategory!=CustomRetry", packageVersion: packageVersion, expectedExitCode: UseMtp ? 2 : 1, useDotnetExec: UseMtp);
            var attempts = File.ReadAllLines(attemptsFile);
            if (UseMtp)
            {
                var history = File.ReadAllLines(historyFile);
                history.Should().Contain("PassesOnThirdAttempt|1|True");
                history.Should().Contain("PassesOnThirdAttempt|2|True");
                history.Should().Contain("PassesOnThirdAttempt|3|False");
                history.Should().Contain("AlwaysFails|1|True");
                history.Should().Contain("AlwaysFails|2|True");
                history.Should().Contain("AlwaysFails|3|False");
            }

            AssertAttempts("PassesImmediately", 1, TestTags.StatusPass);
            AssertAttempts("PassesOnThirdAttempt", 3, TestTags.StatusPass);
            AssertAttempts("AlwaysFails", automaticRetries && totalRetryCount > 1 ? 5 : 3, TestTags.StatusFail);
            AssertAttempts("PassesAfterAwait", 2, TestTags.StatusPass);
            AssertAttempts("PassesAfterClassInitialization", 2, TestTags.StatusPass);
            AssertAttempts("AnotherTestAfterClassInitialization", 2, TestTags.StatusPass);
            AssertAttempts("BecomesInconclusive", 2, TestTags.StatusSkip);
            attempts.Count(line => line.StartsWith("ParameterizedRetry0:", StringComparison.Ordinal)).Should().Be(1);
            attempts.Count(line => line.StartsWith("ParameterizedRetry1:", StringComparison.Ordinal)).Should().Be(2);
            tests.Where(test => test.Resource.EndsWith(".ParameterizedRetry", StringComparison.Ordinal))
                 .GroupBy(test => test.Meta[TestTags.Parameters])
                 .Select(row => row.Count())
                 .Should().BeEquivalentTo([1, 2]);

            void AssertAttempts(string name, int count, string finalStatus)
            {
                attempts.Count(line => line.StartsWith(name + ":", StringComparison.Ordinal)).Should().Be(count, name);
                var executions = tests.Where(test => test.Resource.EndsWith("." + name, StringComparison.Ordinal)).OrderBy(test => test.Start).ToArray();
                executions.Should().HaveCount(count, name);
                executions.Count(test => test.Meta.TryGetValue(TestTags.TestIsRetry, out var isRetry) && isRetry == "true").Should().Be(count - 1, name);
                executions.Count(test => test.Meta.ContainsKey(TestTags.TestFinalStatus)).Should().Be(1, name);
                executions.Last().Meta[TestTags.TestFinalStatus].Should().Be(finalStatus, name);
                foreach (var nativeRetry in executions.Skip(1).Take(Math.Min(count, 3) - 1))
                {
                    nativeRetry.Meta.Should().NotContainKey(TestTags.TestRetryReason);
                }
            }
        }
        finally
        {
            Output.WriteLine(JsonConvert.SerializeObject(tests, Formatting.Indented));
            File.Delete(attemptsFile);
            File.Delete(historyFile);
        }
    }
}

#endif
