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
[Trait("RunOnWindows", "True")]
public class MsTestV2NativeRetriesTests : TestingFrameworkEvpTest
{
    // PackageVersions supplies either the local sample or the versioned 4.4.0 sample.
    private static readonly string PackageVersion = (string)PackageVersions.MSTestNativeRetries.First(version => version[0] is "" or "4.4.0")[0];

    public MsTestV2NativeRetriesTests(ITestOutputHelper output)
        : this("MSTestTestsNativeRetries", output)
    {
    }

    protected MsTestV2NativeRetriesTests(string sample, ITestOutputHelper output)
        : base(sample, output)
    {
    }

    protected virtual bool UseMtp => false;

    private int FailedTestExitCode => UseMtp ? 2 : 1;

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

        using var result = await RunMSTestAsync(agent, "FullyQualifiedName~PassesBeforeCleanupFailure", FailedTestExitCode);
        tests.Should().HaveCount(2);
        tests.Single(test => test.Meta.ContainsKey(TestTags.TestFinalStatus)).Meta[TestTags.TestFinalStatus].Should().Be(TestTags.StatusPass);
        suites.Should().ContainSingle();
        suites.Single()["meta"].Value<string>(TestTags.Status).Should().Be(TestTags.StatusFail);
        suites.Single()["meta"].Value<string>(Tags.ErrorMsg).Should().Contain("Class cleanup failed after the retry.");
    }

    [Fact]
    public async Task NativeRetriesPreserveSkippedAndPassingRows()
    {
        var result = await RunRetryScenarioAsync("MixedRowOutcomes", expectedAttempts: 4, expectedRows: 2, expectedExitCode: 0);
        result.Tests.Where(test => test.Meta.ContainsKey(TestTags.TestFinalStatus))
              .Select(test => test.Meta[TestTags.TestFinalStatus])
              .Should().BeEquivalentTo(TestTags.StatusSkip, TestTags.StatusPass);
    }

    [Fact]
    public async Task NativeRetriesExhaustTimeoutAttempts()
    {
        var result = await RunRetryScenarioAsync("TimesOut", expectedAttempts: 3, expectedRows: 1, expectedExitCode: FailedTestExitCode);
        result.Tests.Should().OnlyContain(test => test.Meta[TestTags.Status] == TestTags.StatusFail);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeRetriesKeepEachRowsFinalOutcome(bool automaticRetries)
    {
        var result = await RunRetryScenarioAsync(
            "RegressesAfterPassing",
            expectedAttempts: automaticRetries ? 7 : 6,
            expectedRows: 2,
            expectedExitCode: automaticRetries ? 0 : FailedTestExitCode,
            automaticRetries: automaticRetries);
        var rows = result.Tests.GroupBy(test => test.Meta[TestTags.Parameters]).ToArray();
        rows.Should().HaveCount(2);
        foreach (var row in rows)
        {
            var attempts = row.OrderBy(test => test.Start).ToArray();
            attempts.Length.Should().BeInRange(3, automaticRetries ? 4 : 3);
            attempts.Take(attempts.Length - 1).Should().OnlyContain(test => !test.Meta.ContainsKey(TestTags.TestFinalStatus));
            attempts.Last().Meta[TestTags.TestFinalStatus].Should().Be(attempts.Last().Meta[TestTags.Status]);
        }

        result.Tests.Where(test => test.Meta.ContainsKey(TestTags.TestFinalStatus))
              .Select(test => test.Meta[TestTags.TestFinalStatus])
              .Should().BeEquivalentTo(automaticRetries ? TestTags.StatusPass : TestTags.StatusFail, TestTags.StatusPass);
        if (UseMtp)
        {
            result.RetryHistory.Should().BeEquivalentTo(
                "RegressesAfterPassing (0)|1|True",
                "RegressesAfterPassing (1)|1|True",
                "RegressesAfterPassing (0)|2|True",
                "RegressesAfterPassing (1)|2|True",
                "RegressesAfterPassing (0)|3|False",
                "RegressesAfterPassing (1)|3|False");
        }
    }

    [Fact]
    public async Task NativeAndAutomaticRetriesUseFreshInstances()
    {
        var result = await RunRetryScenarioAsync("RetriesUseFreshInstances", expectedAttempts: 4, expectedRows: 1, expectedExitCode: 0, automaticRetries: true);
        result.Tests.OrderBy(test => test.Start).Last().Meta[TestTags.TestFinalStatus].Should().Be(TestTags.StatusPass);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomRetryPolicyCanContinueAfterPassing(bool automaticRetries)
    {
        var result = await RunRetryScenarioAsync(
            "CustomPolicyContinuesAfterPassing",
            expectedAttempts: automaticRetries ? 4 : 3,
            expectedRows: 1,
            expectedExitCode: automaticRetries ? 0 : FailedTestExitCode,
            automaticRetries: automaticRetries);
        result.Tests.OrderBy(test => test.Start).Last().Meta[TestTags.TestFinalStatus].Should().Be(automaticRetries ? TestTags.StatusPass : TestTags.StatusFail);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomRetryPolicyCanSelectAnEarlierPassingAttempt(bool automaticRetries)
    {
        var result = await RunRetryScenarioAsync("CustomPolicySelectsEarlierAttempt", expectedAttempts: 3, expectedRows: 1, expectedExitCode: 0, automaticRetries: automaticRetries);
        result.Tests.OrderBy(test => test.Start).Select(test => test.Meta[TestTags.Status]).Should().Equal(TestTags.StatusFail, TestTags.StatusPass, TestTags.StatusFail);
        result.Tests.Single(test => test.Meta.ContainsKey(TestTags.TestFinalStatus)).Meta[TestTags.TestFinalStatus].Should().Be(TestTags.StatusPass);
        if (UseMtp)
        {
            result.RetryHistory.Should().BeEquivalentTo("CustomPolicySelectsEarlierAttempt|1|True", "CustomPolicySelectsEarlierAttempt|2|False");
        }
    }

    [Fact]
    public async Task DelegatingExecutorPreservesNativeRetryResults()
    {
        var result = await RunRetryScenarioAsync("DelegatingExecutor", expectedAttempts: 2, expectedRows: 1, expectedExitCode: 0);
        result.Tests.OrderBy(test => test.Start).Last().Meta[TestTags.TestFinalStatus].Should().Be(TestTags.StatusPass);
    }

    [Fact]
    public async Task MethodRetryPolicyOverridesTheClassPolicy()
    {
        var result = await RunRetryScenarioAsync("MethodRetryOverridesClass", expectedAttempts: 2, expectedRows: 1, expectedExitCode: FailedTestExitCode);
        result.Tests.Should().OnlyContain(test => test.Meta[TestTags.Status] == TestTags.StatusFail);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MultipleExecutorResultsKeepSeparateRetriesAndIdentities(bool automaticRetries)
    {
        var result = await RunRetryScenarioAsync(
            "MultipleResults",
            expectedAttempts: automaticRetries ? 7 : 6,
            expectedRows: 2,
            expectedExitCode: automaticRetries ? 0 : FailedTestExitCode,
            automaticRetries: automaticRetries);
        result.Tests.Where(test => test.Meta.ContainsKey(TestTags.TestFinalStatus))
              .Select(test => test.Meta[TestTags.TestFinalStatus])
              .Should().BeEquivalentTo(automaticRetries ? TestTags.StatusPass : TestTags.StatusFail, TestTags.StatusPass);
        result.Tests.Select(test => test.Meta[TestTags.Name]).Distinct().Should().BeEquivalentTo("First result", "Second result");
        if (UseMtp)
        {
            result.RetryHistory.Should().BeEquivalentTo(
                "First result|1|True",
                "Second result|1|True",
                "First result|2|True",
                "Second result|2|True",
                "First result|3|False",
                "Second result|3|False");
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
        using var result = await RunMSTestAsync(agent, $"FullyQualifiedName~.{testName}", UseMtp ? mtpExitCode : vstestExitCode);
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
    [InlineData("efd", "CustomPolicySelectsEarlierAttempt", 5, "pass", 3)]
    [InlineData("attempt_to_fix", "CustomPolicySelectsEarlierAttempt", 5, "fail", 3)]
    public async Task NativeRetriesRespectTestOptimizationPolicies(string feature, string name, int expectedAttempts, string expectedFinalStatus, int expectedNativeAttempts)
    {
        EnvironmentHelper.EnableDefaultTransport();
        InjectSession(out _, out _, out _, out _, out _, out _, out _);
        var isEfd = feature.StartsWith("efd", StringComparison.Ordinal);
        var isAtr = feature == "efd_and_atr";
        var isItr = feature is "itr" or "itr_row";
        var skipOneRow = feature == "itr_row";
        var module = UseMtp ? "Samples.MSTestTestsNativeRetriesMtp" : "Samples.MSTestTestsNativeRetries";
        var suite = name == "CustomPolicySelectsEarlierAttempt" ? "Samples.MSTestTestsNativeRetries.CustomRetryTestSuite" : "Samples.MSTestTestsNativeRetries.TestSuite";
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
                                    suite,
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
                                        [suite] = new
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
            using var result = await RunMSTestAsync(agent, $"FullyQualifiedName~{name}", isEfd && expectedFinalStatus == TestTags.StatusFail ? FailedTestExitCode : 0);
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

            if (name == "CustomPolicySelectsEarlierAttempt")
            {
                tests.OrderBy(test => test.Start).Select(test => test.Meta[TestTags.Status]).Should().Equal(TestTags.StatusFail, TestTags.StatusPass, TestTags.StatusFail, TestTags.StatusPass, TestTags.StatusPass);
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
    [InlineData(false, 1000)]
    [InlineData(true, 1000)]
    [InlineData(true, 1)]
    public async Task NativeRetriesCompleteBeforeAutomaticRetries(bool automaticRetries, int totalRetryCount)
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
            using var result = await RunMSTestAsync(agent, "TestCategory!=CustomRetry", FailedTestExitCode);
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

    private async Task<RetryScenarioResult> RunRetryScenarioAsync(string name, int expectedAttempts, int expectedRows, int expectedExitCode, bool automaticRetries = false)
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
            using var result = await RunMSTestAsync(agent, $"FullyQualifiedName~{name}", expectedExitCode);
            File.ReadAllLines(attemptsFile).Should().HaveCount(expectedAttempts);
            tests.Should().HaveCount(expectedAttempts);
            tests.Count(test => test.Meta.ContainsKey(TestTags.TestFinalStatus)).Should().Be(expectedRows);
            tests.Count(test => test.Meta.ContainsKey(TestTags.TestIsRetry)).Should().Be(expectedAttempts - expectedRows);
            return new RetryScenarioResult(tests, File.ReadAllLines(historyFile));
        }
        finally
        {
            Output.WriteLine(JsonConvert.SerializeObject(tests, Formatting.Indented));
            File.Delete(attemptsFile);
            File.Delete(historyFile);
        }
    }

    private Task<ProcessResult> RunMSTestAsync(MockTracerAgent agent, string testFilter, int expectedExitCode)
    {
        var arguments = UseMtp ? "--filter " + testFilter : "--TestCaseFilter:" + testFilter;
#if NETFRAMEWORK
        // Visual Studio can launch a 64-bit test host even when the fixture uses the x86 profiler.
        arguments += " /Platform:" + EnvironmentTools.GetTestTargetPlatform();
#endif
        return RunDotnetTestSampleAndWaitForExit(agent, arguments: arguments, packageVersion: PackageVersion, expectedExitCode: expectedExitCode, useDotnetExec: UseMtp);
    }

    private readonly struct RetryScenarioResult(IReadOnlyList<MockCIVisibilityTest> tests, string[] retryHistory)
    {
        public IReadOnlyList<MockCIVisibilityTest> Tests { get; } = tests;

        public string[] RetryHistory { get; } = retryHistory;
    }
}

#endif
