// <copyright file="TestingFrameworkRetriesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP3_1_OR_GREATER
using System;
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
public abstract class TestingFrameworkRetriesTests : TestingFrameworkEvpTest
{
    public TestingFrameworkRetriesTests(string sampleAppName, ITestOutputHelper output)
        : base(sampleAppName, output)
    {
        SetServiceVersion("1.0.0");
    }

    protected virtual string SettingsJson { get; } = """{"data":{"id":"511938a3f19c12f8bb5e5caa695ca24f4563de3f","type":"ci_app_tracers_test_service_settings","attributes":{"code_coverage":false,"early_flake_detection":{"enabled":false,"slow_test_retries":{"10s":5,"30s":3,"5m":2,"5s":10},"faulty_session_threshold":100},"flaky_test_retries_enabled":true,"itr_enabled":false,"require_git":false,"tests_skipping":false}}}""";

    protected abstract string AlwaysFails { get; }

    protected abstract string AlwaysPasses { get; }

    protected abstract string TrueAtLastRetry { get; }

    protected abstract string TrueAtThirdRetry { get; }

    protected virtual int ExpectedTestSuiteCount => 1;

    protected virtual bool UseDotnetExec => false;

    protected virtual string[] QuarantineTestNames => ["AlwaysFails", "AlwaysPasses", "TrueAtLastRetry", "TrueAtThirdRetry"];

    public virtual async Task QuarantineWithAutomaticRetries(string packageVersion, bool quarantined, bool retriesEnabled, bool quarantineAlwaysFails)
    {
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.FlakyRetryEnabled, retriesEnabled ? "1" : "0");
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.FlakyRetryCount, "5");

        var testNames = QuarantineTestNames;
        var moduleName = EnvironmentHelper.FullSampleName;
        var suiteName = AlwaysFails.Substring(0, AlwaysFails.LastIndexOf('.'));
        var managementTests = JsonConvert.SerializeObject(new
        {
            data = new
            {
                attributes = new
                {
                    modules = new Dictionary<string, object>
                    {
                        [moduleName] = new
                        {
                            suites = new Dictionary<string, object>
                            {
                                [suiteName] = new
                                {
                                    tests = testNames.ToDictionary(name => name, name => new { properties = new { quarantined = IsQuarantined(name) } })
                                }
                            }
                        }
                    }
                }
            }
        });

        await ExecuteTestAsync(
            packageVersion,
            "evp_proxy/v4",
            false,
            new TestScenario(
                GetType().Name,
                nameof(QuarantineWithAutomaticRetries),
                new MockData(GetSettingsJson("false", "false", "true", "0", retriesEnabled ? "true" : "false"), string.Empty, managementTests),
                expectedExitCode: quarantined && quarantineAlwaysFails ? 0 : 1,
                expectedSpans: testNames.Sum(ExpectedExecutions),
                useSnapshot: false,
                validateAction: (in ExecutionData data) =>
                {
                    Assert.Single(data.TestModules);
                    Assert.Single(data.TestSuites);
                    foreach (var name in testNames)
                    {
                        var executions = data.Tests.Where(test => test.Meta[TestTags.Name] == name).ToList();
                        var expectedExecutions = ExpectedExecutions(name);
                        executions.Should().HaveCount(expectedExecutions);
                        var retries = executions.Where(test => test.Meta.TryGetValue(TestTags.TestIsRetry, out var retry) && retry == "true").ToList();
                        retries.Should().HaveCount(expectedExecutions - 1);
                        foreach (var retry in retries)
                        {
                            retry.Meta[TestTags.TestRetryReason].Should().Be(TestTags.TestRetryReasonAtr);
                        }

                        // Quarantine must preserve the actual execution outcomes sent to Datadog.
                        var passes = name == "AlwaysPasses" || (retriesEnabled && (name == "TrueAtLastRetry" || name == "TrueAtThirdRetry")) ? 1 : 0;
                        executions.Count(test => test.Meta[TestTags.Status] == TestTags.StatusPass).Should().Be(passes);
                        executions.Count(test => test.Meta[TestTags.Status] == TestTags.StatusFail).Should().Be(expectedExecutions - passes);
                        executions.Count(test => test.Meta.TryGetValue(TestTags.TestIsQuarantined, out var value) && value == "true").Should().Be(IsQuarantined(name) ? expectedExecutions : 0);

                        var finalExecution = Assert.Single(executions, test => test.Meta.ContainsKey(TestTags.TestFinalStatus));
                        finalExecution.Meta[TestTags.TestFinalStatus].Should().Be(IsQuarantined(name) ? TestTags.StatusSkip : passes > 0 ? TestTags.StatusPass : TestTags.StatusFail);
                    }
                },
                useDotnetExec: UseDotnetExec));

        int ExpectedExecutions(string name) => !retriesEnabled || name == "AlwaysPasses" ? 1 : name == "TrueAtThirdRetry" ? 4 : 6;

        bool IsQuarantined(string name) => quarantined && (name != "AlwaysFails" || quarantineAlwaysFails);
    }

    public virtual Task<List<MockCIVisibilityTest>> FlakyRetries(string packageVersion)
        => FlakyRetriesWithArguments(packageVersion, arguments: null);

    public virtual Task FlakyRetriesWithExceptionReplay(string packageVersion)
        => FlakyRetriesWithExceptionReplayCore(packageVersion);

    protected static IEnumerable<object[]> GetQuarantineRetryData(IEnumerable<object[]> packageVersions)
    {
        foreach (var version in packageVersions)
        {
            foreach (var quarantined in new[] { false, true })
            {
                foreach (var retriesEnabled in new[] { false, true })
                {
                    yield return [version[0], quarantined, retriesEnabled, true];
                }
            }

            // A failure outside quarantine must still fail the process when other tests are quarantined.
            yield return [version[0], true, true, false];
        }
    }

    protected async Task<List<MockCIVisibilityTest>> FlakyRetriesWithArguments(string packageVersion, string arguments)
    {
        EnvironmentHelper.EnableDefaultTransport();
        var tests = new List<MockCIVisibilityTest>();
        var testSuites = new List<MockCIVisibilityTestSuite>();
        var testModules = new List<MockCIVisibilityTestModule>();

        InjectSession(
            out var sessionId,
            out var sessionCommand,
            out var sessionWorkingDirectory,
            out var gitRepositoryUrl,
            out var gitBranch,
            out var gitCommitSha,
            out var runId);

        Output.WriteLine("RunId: {0}", runId);
        try
        {
            var retryCount = 5;
            SetEnvironmentVariable(ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "1");
            SetEnvironmentVariable(ConfigurationKeys.CIVisibility.FlakyRetryCount, retryCount.ToString());

            using var agent = EnvironmentHelper.GetMockAgent();
            agent.EventPlatformProxyPayloadReceived += (sender, e) =>
            {
                if (e.Value.PathAndQuery.EndsWith("api/v2/libraries/tests/services/setting"))
                {
                    e.Value.Response = new MockTracerResponse(SettingsJson, 200);
                    return;
                }

                if (e.Value.PathAndQuery.EndsWith("api/v2/citestcycle"))
                {
                    var payload = JsonConvert.DeserializeObject<MockCIVisibilityProtocol>(e.Value.BodyInJson);
                    if (payload.Events?.Length > 0)
                    {
                        foreach (var @event in payload.Events)
                        {
                            if (@event.Content.ToString() is { } eventContent)
                            {
                                if (@event.Type == SpanTypes.Test)
                                {
                                    tests.Add(JsonConvert.DeserializeObject<MockCIVisibilityTest>(eventContent));
                                }
                                else if (@event.Type == SpanTypes.TestSuite)
                                {
                                    testSuites.Add(JsonConvert.DeserializeObject<MockCIVisibilityTestSuite>(eventContent));
                                }
                                else if (@event.Type == SpanTypes.TestModule)
                                {
                                    testModules.Add(JsonConvert.DeserializeObject<MockCIVisibilityTestModule>(eventContent));
                                }
                            }
                        }
                    }
                }
            };

            using var processResult = await RunDotnetTestSampleAndWaitForExit(
                                          agent,
                                          packageVersion: packageVersion,
                                          expectedExitCode: 1,
                                          useDotnetExec: UseDotnetExec,
                                          arguments: arguments);

            // 1 Module
            testModules.Should().HaveCount(1);

            testSuites.Should().HaveCount(ExpectedTestSuiteCount);

            // AlwaysFails => 1 + 5 retries
            var alwaysFailsTests = tests.Where(t => t.Resource == AlwaysFails).ToList();
            alwaysFailsTests.Should().HaveCount(1 + retryCount);
            alwaysFailsTests.Should().OnlyContain(t => t.Meta[TestTags.Status] == TestTags.StatusFail);

            // AlwaysPasses => 1
            var alwaysPassesTests = tests.Where(t => t.Resource == AlwaysPasses).ToList();
            alwaysPassesTests.Should().HaveCount(1);
            alwaysPassesTests.Should().OnlyContain(t => t.Meta[TestTags.Status] == TestTags.StatusPass);

            // TrueAtLastRetry => 1 + 5 retries
            var trueAtLastRetryTests = tests.Where(t => t.Resource == TrueAtLastRetry).ToList();
            trueAtLastRetryTests.Should().HaveCount(1 + retryCount);
            trueAtLastRetryTests.Should().Contain(t => t.Meta[TestTags.Status] == TestTags.StatusPass);

            // TrueAtThirdRetry => 1 + 3 retries
            var trueAtThirdRetryTests = tests.Where(t => t.Resource == TrueAtThirdRetry).ToList();
            trueAtThirdRetryTests.Should().HaveCount(1 + 3);
            trueAtThirdRetryTests.Should().Contain(t => t.Meta[TestTags.Status] == TestTags.StatusPass);
        }
        catch
        {
            WriteSpans(tests);
            throw;
        }

        return tests;
    }

    private static void CheckForEnoughNumberOfStackFrames(List<MockCIVisibilityTest> tests)
    {
        Skip.If(
            tests.Any(t => NumberOfOccurrences(t.Meta["error.stack"], "   at ") == 1),
            "There are stacktraces with only 1 stackframe, these kind of exception doesn't have any debugger info because that stack frame always refers to the throw frame.");

        Skip.If(
            tests.Any(t => NumberOfOccurrences(t.Meta["error.stack"], "   at ") == NumberOfOccurrences(t.Meta["error.stack"], "   at Xunit.")),
            "All stackframes in the exception contains information of the Xunit assembly only.");
    }

    private static int NumberOfOccurrences(ReadOnlySpan<char> value, ReadOnlySpan<char> pattern, bool allowOverlap = false)
    {
        if (pattern.IsEmpty)
        {
            return 0; // empty pattern has 0 occurrences
        }

        var count = 0;
        var shift = allowOverlap ? 1 : pattern.Length;
        while (value.Length >= pattern.Length)
        {
            var pos = value.IndexOf(pattern);
            if (pos < 0)
            {
                break;
            }

            count++;
            value = value.Slice(pos + shift);
        }

        return count;
    }

    private async Task FlakyRetriesWithExceptionReplayCore(string packageVersion)
    {
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.DynamicInstrumentationEnabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.ExceptionReplayEnabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.RateLimitSeconds, "0");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.UploadFlushInterval, "1000");

        var tests = await FlakyRetries(packageVersion);

        try
        {
            // AlwaysFails => 1 + 5 retries
            var alwaysFailsTests = tests.Where(t => t.Resource == AlwaysFails).ToList();
            alwaysFailsTests.Should().Contain(t => t.Meta[TestTags.Status] == TestTags.StatusFail);
            alwaysFailsTests = alwaysFailsTests.Where(t => t.Meta.ContainsKey("error.stack")).ToList();
            CheckForEnoughNumberOfStackFrames(alwaysFailsTests);
            alwaysFailsTests.Should().Contain(t => t.Meta.ContainsKey("_dd.di._er"));
            alwaysFailsTests.Should().Contain(t => t.Meta.ContainsKey("_dd.di._eh"));
            alwaysFailsTests.Should().Contain(t => t.Meta.ContainsKey("error.debug_info_captured"));
            alwaysFailsTests.Should().Contain(t => t.Meta.ContainsKey("_dd.debug.error.exception_hash"));
            alwaysFailsTests.Should().Contain(t => t.Meta.ContainsKey("_dd.debug.error.exception_id"));

            // AlwaysPasses => 1
            var alwaysPassesTests = tests.Where(t => t.Resource == AlwaysPasses).ToList();
            alwaysPassesTests.Should().HaveCount(1);
            alwaysPassesTests.Should().OnlyContain(t => t.Meta[TestTags.Status] == TestTags.StatusPass);
            alwaysPassesTests.Should().NotContain(t => t.Meta.ContainsKey("_dd.di._er"));
            alwaysPassesTests.Should().NotContain(t => t.Meta.ContainsKey("_dd.di._eh"));
            alwaysPassesTests.Should().NotContain(t => t.Meta.ContainsKey("error.debug_info_captured"));
            alwaysPassesTests.Should().NotContain(t => t.Meta.ContainsKey("_dd.debug.error.exception_hash"));
            alwaysPassesTests.Should().NotContain(t => t.Meta.ContainsKey("_dd.debug.error.exception_id"));

            // TrueAtLastRetry => 1 + 5 retries
            var trueAtLastRetryTests = tests.Where(t => t.Resource == TrueAtLastRetry).ToList();
            trueAtLastRetryTests.Should().Contain(t => t.Meta[TestTags.Status] == TestTags.StatusPass);
            trueAtLastRetryTests.Should().Contain(t => t.Meta[TestTags.Status] == TestTags.StatusFail);
            trueAtLastRetryTests = trueAtLastRetryTests.Where(t => t.Meta.ContainsKey("error.stack")).ToList();
            CheckForEnoughNumberOfStackFrames(trueAtLastRetryTests);
            trueAtLastRetryTests.Should().Contain(t => t.Meta.ContainsKey("_dd.di._er"));
            trueAtLastRetryTests.Should().Contain(t => t.Meta.ContainsKey("_dd.di._eh"));
            trueAtLastRetryTests.Should().Contain(t => t.Meta.ContainsKey("error.debug_info_captured"));
            trueAtLastRetryTests.Should().Contain(t => t.Meta.ContainsKey("_dd.debug.error.exception_hash"));
            trueAtLastRetryTests.Should().Contain(t => t.Meta.ContainsKey("_dd.debug.error.exception_id"));

            // TrueAtThirdRetry => 1 + 3 retries
            var trueAtThirdRetryTests = tests.Where(t => t.Resource == TrueAtThirdRetry).ToList();
            trueAtThirdRetryTests.Should().HaveCount(1 + 3);
            trueAtThirdRetryTests.Should().Contain(t => t.Meta[TestTags.Status] == TestTags.StatusPass);
            trueAtThirdRetryTests.Should().Contain(t => t.Meta[TestTags.Status] == TestTags.StatusFail);
            trueAtThirdRetryTests = trueAtThirdRetryTests.Where(t => t.Meta.ContainsKey("error.stack")).ToList();
            CheckForEnoughNumberOfStackFrames(trueAtThirdRetryTests);
            trueAtThirdRetryTests.Should().Contain(t => t.Meta.ContainsKey("_dd.di._er"));
            trueAtThirdRetryTests.Should().Contain(t => t.Meta.ContainsKey("_dd.di._eh"));
            trueAtThirdRetryTests.Should().Contain(t => t.Meta.ContainsKey("error.debug_info_captured"));
            trueAtThirdRetryTests.Should().Contain(t => t.Meta.ContainsKey("_dd.debug.error.exception_hash"));
            trueAtThirdRetryTests.Should().Contain(t => t.Meta.ContainsKey("_dd.debug.error.exception_id"));
        }
        catch
        {
            Output.WriteLine("Tests (in JSON):\n{0}", JsonConvert.SerializeObject(tests,  Formatting.Indented));
            throw;
        }
    }
}
#endif
