// <copyright file="TestingFrameworkEvpTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

public abstract class TestingFrameworkEvpTest : TestHelper
{
    private readonly GacFixture _gacFixture;

    protected TestingFrameworkEvpTest(string sampleAppName, string samplePathOverrides, ITestOutputHelper output)
        : base(sampleAppName, samplePathOverrides, output)
    {
        SetCIEnvironmentValues();
        _gacFixture = new GacFixture();
        _gacFixture.AddAssembliesToGac();
    }

    protected TestingFrameworkEvpTest(string sampleAppName, string samplePathOverrides, ITestOutputHelper output, bool prependSamplesToAppName)
        : base(sampleAppName, samplePathOverrides, output, prependSamplesToAppName)
    {
        SetCIEnvironmentValues();
        _gacFixture = new GacFixture();
        _gacFixture.AddAssembliesToGac();
    }

    protected TestingFrameworkEvpTest(string sampleAppName, ITestOutputHelper output)
        : base(sampleAppName, output)
    {
        SetCIEnvironmentValues();
        _gacFixture = new GacFixture();
        _gacFixture.AddAssembliesToGac();
    }

    protected TestingFrameworkEvpTest(EnvironmentHelper environmentHelper, ITestOutputHelper output)
        : base(environmentHelper, output)
    {
        SetCIEnvironmentValues();
        _gacFixture = new GacFixture();
        _gacFixture.AddAssembliesToGac();
    }

    protected object? CIValues { get; private set; }

    public override void Dispose()
    {
        _gacFixture.RemoveAssembliesFromGac();
    }

    protected static string GetSettingsJson(string earlyFlakeDetection, string testsSkipping, string testManagementEnabled, string attemptToFixRetries)
    {
        return $$"""
                 {
                     "data": {
                         "id": "511938a3f19c12f8bb5e5caa695ca24f4563de3f",
                         "type": "ci_app_tracers_test_service_settings",
                         "attributes": {
                             "code_coverage": false,
                             "early_flake_detection": {
                                 "enabled": {{earlyFlakeDetection}},
                                 "slow_test_retries": {
                                     "10s": 10,
                                     "30s": 10,
                                     "5m": 10,
                                     "5s": 10
                                 },
                                 "faulty_session_threshold": 100
                             },
                             "flaky_test_retries_enabled": false,
                             "itr_enabled": true,
                             "require_git": false,
                             "tests_skipping": {{testsSkipping}},
                             "known_tests_enabled": {{earlyFlakeDetection}},
                             "test_management": {
                                 "enabled": {{testManagementEnabled}},
                                 "attempt_to_fix_retries": {{attemptToFixRetries}}
                             }
                         }
                     }
                 }
                 """;
    }

    protected virtual void WriteSpans(List<MockCIVisibilityTest>? tests)
    {
        if (tests is null || tests.Count == 0)
        {
            return;
        }

        var sb = StringBuilderCache.Acquire();
        sb.AppendLine("***********************************");

        var i = 0;
        foreach (var test in tests)
        {
            sb.Append($" {i++}) ");
            sb.Append($"TraceId={test.TraceId}, ");
            sb.Append($"SpanId={test.SpanId}, ");
            sb.Append($"Service={test.Service}, ");
            sb.Append($"Name={test.Name}, ");
            sb.Append($"Resource={test.Resource}, ");
            sb.Append($"Type={test.Type}, ");
            sb.Append($"Error={test.Error}");
            sb.AppendLine();
            sb.AppendLine("   Tags=");
            foreach (var kv in test.Meta.OrderBy(i => i.Key))
            {
                sb.AppendLine($"       => {kv.Key} = {kv.Value}");
            }

            sb.AppendLine("   Metrics=");
            foreach (var kv in test.Metrics.OrderBy(i => i.Key))
            {
                sb.AppendLine($"       => {kv.Key} = {kv.Value}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("***********************************");
        Output.WriteLine(StringBuilderCache.GetStringAndRelease(sb));
    }

    protected virtual void WriteSpans(List<MockCIVisibilityTestSuite>? suites)
    {
        if (suites is null || suites.Count == 0)
        {
            return;
        }

        var sb = StringBuilderCache.Acquire();
        sb.AppendLine("***********************************");

        var i = 0;
        foreach (var suite in suites)
        {
            sb.Append($" {i++}) ");
            sb.Append($"TestSuiteId={suite.TestSuiteId}, ");
            sb.Append($"Service={suite.Service}, ");
            sb.Append($"Name={suite.Name}, ");
            sb.Append($"Resource={suite.Resource}, ");
            sb.Append($"Type={suite.Type}, ");
            sb.Append($"Error={suite.Error}");
            sb.AppendLine();
            sb.AppendLine("   Tags=");
            foreach (var kv in suite.Meta.OrderBy(i => i.Key))
            {
                sb.AppendLine($"       => {kv.Key} = {kv.Value}");
            }

            sb.AppendLine("   Metrics=");
            foreach (var kv in suite.Metrics.OrderBy(i => i.Key))
            {
                sb.AppendLine($"       => {kv.Key} = {kv.Value}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("***********************************");
        Output.WriteLine(StringBuilderCache.GetStringAndRelease(sb));
    }

    protected virtual string AssertTargetSpanAnyOf(MockCIVisibilityTest targetTest, string key, params string[] values)
    {
        var actualValue = targetTest.Meta[key];
        values.Should().Contain(actualValue);
        targetTest.Meta.Remove(key);
        return actualValue;
    }

    protected virtual void AssertTargetSpanEqual(MockCIVisibilityTest targetTest, string key, string value)
    {
        targetTest.Meta[key].Should().Be(value);
        targetTest.Meta.Remove(key);
    }

    protected virtual void AssertTargetSpanExists(MockCIVisibilityTest targetTest, string key)
    {
        targetTest.Meta.Should().ContainKey(key);
        targetTest.Meta.Remove(key);
    }

    protected virtual void AssertTargetSpanContains(MockCIVisibilityTest targetTest, string key, string value)
    {
        targetTest.Meta[key].Should().Contain(value);
        targetTest.Meta.Remove(key);
    }

    protected virtual void CheckRuntimeValues(MockCIVisibilityTest targetTest)
    {
        AssertTargetSpanExists(targetTest, CommonTags.RuntimeName);
        AssertTargetSpanExists(targetTest, CommonTags.RuntimeVersion);
        AssertTargetSpanExists(targetTest, CommonTags.RuntimeArchitecture);
        AssertTargetSpanExists(targetTest, CommonTags.OSArchitecture);
        AssertTargetSpanExists(targetTest, CommonTags.OSPlatform);
        AssertTargetSpanEqual(targetTest, CommonTags.OSVersion, new TestOptimizationHostInfo().GetOperatingSystemVersion() ?? string.Empty);
        targetTest.Metrics[CommonTags.LogicalCpuCount].Should().Be(Environment.ProcessorCount);
        targetTest.Metrics.Remove(CommonTags.LogicalCpuCount);
    }

    protected virtual void CheckOriginTag(MockCIVisibilityTest targetTest)
    {
        // Check the test origin tag
        AssertTargetSpanEqual(targetTest, Tags.Origin, TestTags.CIAppTestOriginName);
    }

    protected virtual void CheckCIEnvironmentValuesDecoration(MockCIVisibilityTest targetTest, string? repository = null, string? branch = null, string? commitSha = null)
    {
        var context = new SpanContext(parent: null, traceContext: null, serviceName: null);
        var span = new Span(context, DateTimeOffset.UtcNow);
        ((CIEnvironmentValues?)CIValues)?.DecorateSpan(span);

        AssertEqual(CommonTags.CIProvider);
        AssertEqual(CommonTags.CIPipelineId);
        AssertEqual(CommonTags.CIPipelineName);
        AssertEqual(CommonTags.CIPipelineNumber);
        AssertEqual(CommonTags.CIPipelineUrl);
        AssertEqual(CommonTags.CIJobUrl);
        AssertEqual(CommonTags.CIJobName);
        AssertEqual(CommonTags.StageName);
        AssertEqual(CommonTags.CIWorkspacePath);
        AssertEqual(CommonTags.GitRepository, repository);
        AssertEqual(CommonTags.GitCommit, commitSha);
        AssertEqual(CommonTags.GitBranch, branch);
        AssertEqual(CommonTags.GitTag);
        AssertEqual(CommonTags.GitCommitAuthorName);
        AssertEqual(CommonTags.GitCommitAuthorEmail);
        AssertEqualDate(CommonTags.GitCommitAuthorDate);
        AssertEqual(CommonTags.GitCommitCommitterName);
        AssertEqual(CommonTags.GitCommitCommitterEmail);
        AssertEqualDate(CommonTags.GitCommitCommitterDate);
        AssertEqual(CommonTags.GitCommitMessage);
        AssertEqual(CommonTags.BuildSourceRoot);
        AssertEqual(CommonTags.CiEnvVars);

        void AssertEqual(string key, string? value = null)
        {
            if (value is null)
            {
                if (span.GetTag(key) is { } tagValue)
                {
                    targetTest.Meta[key].Should().Be(tagValue);
                    targetTest.Meta.Remove(key);
                }
            }
            else
            {
                targetTest.Meta[key].Should().Be(value);
                targetTest.Meta.Remove(key);
            }
        }

        void AssertEqualDate(string key)
        {
            if (span.GetTag(key) is { } keyValue)
            {
                DateTimeOffset.Parse(targetTest.Meta[key]).Should().Be(DateTimeOffset.Parse(keyValue));
                targetTest.Meta.Remove(key);
            }
        }
    }

    protected virtual void CheckTraitsValues(MockCIVisibilityTest targetTest)
    {
        // Check the traits tag value
        AssertTargetSpanEqual(targetTest, TestTags.Traits, "{\"Category\":[\"Category01\"],\"Compatibility\":[\"Windows\",\"Linux\"]}");
    }

    protected virtual void CheckSimpleTestSpan(MockCIVisibilityTest targetTest)
    {
        // Check the Test Status
        AssertTargetSpanEqual(targetTest, TestTags.Status, TestTags.StatusPass);
    }

    protected virtual void CheckSimpleSkipFromAttributeTest(MockCIVisibilityTest targetTest, string skipReason = "Simple skip reason")
    {
        // Check the Test Status
        AssertTargetSpanEqual(targetTest, TestTags.Status, TestTags.StatusSkip);

        // Check the Test skip reason
        AssertTargetSpanEqual(targetTest, TestTags.SkipReason, skipReason);
    }

    protected virtual void CheckSimpleErrorTest(MockCIVisibilityTest targetTest)
    {
        // Check the Test Status
        AssertTargetSpanEqual(targetTest, TestTags.Status, TestTags.StatusFail);

        // Check the span error flag
        targetTest.Error.Should().Be(1);

        // Check the error type
        AssertTargetSpanEqual(targetTest, Tags.ErrorType, typeof(DivideByZeroException).FullName ?? nameof(DivideByZeroException));

        // Check the error stack
        AssertTargetSpanContains(targetTest, Tags.ErrorStack, "at");

        // Check the error message
        AssertTargetSpanEqual(targetTest, Tags.ErrorMsg, new DivideByZeroException().Message);
    }

    protected void SetCIEnvironmentValues()
    {
        var current = GitInfo.GetCurrent();
        var ciDictionaryValues = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.Azure.TFBuild] = "1",
            [PlatformKeys.Ci.Azure.SystemTeamProjectId] = "TeamProjectId",
            [PlatformKeys.Ci.Azure.BuildBuildId] = "BuildId",
            [PlatformKeys.Ci.Azure.SystemJobId] = "JobId",
            [PlatformKeys.Ci.Azure.BuildSourcesDirectory] = current.SourceRoot ?? string.Empty,
            [PlatformKeys.Ci.Azure.BuildDefinitionName] = "DefinitionName",
            [PlatformKeys.Ci.Azure.SystemTeamFoundationServerUri] = "https://foundation.server.url/",
            [PlatformKeys.Ci.Azure.SystemStageDisplayName] = "StageDisplayName",
            [PlatformKeys.Ci.Azure.SystemJobDisplayName] = "JobDisplayName",
            [PlatformKeys.Ci.Azure.SystemTaskInstanceId] = "TaskInstanceId",
            [PlatformKeys.Ci.Azure.SystemPullRequestSourceRepositoryUri] = "git@github.com:DataDog/dd-trace-dotnet.git",
            [PlatformKeys.Ci.Azure.BuildRepositoryUri] = "git@github.com:DataDog/dd-trace-dotnet.git",
            [PlatformKeys.Ci.Azure.SystemPullRequestSourceCommitId] = "3245605c3d1edc67226d725799ee969c71f7632b",
            [PlatformKeys.Ci.Azure.BuildSourceVersion] = "3245605c3d1edc67226d725799ee969c71f7632b",
            [PlatformKeys.Ci.Azure.SystemPullRequestSourceBranch] = "main",
            [PlatformKeys.Ci.Azure.BuildSourceBranch] = "main",
            [PlatformKeys.Ci.Azure.BuildSourceBranchName] = "main",
            [PlatformKeys.Ci.Azure.BuildSourceVersionMessage] = "Fake commit for testing",
            [PlatformKeys.Ci.Azure.BuildRequestedForId] = "AuthorName",
            [PlatformKeys.Ci.Azure.BuildRequestedForEmail] = "author@company.com",
        };

        foreach (var item in ciDictionaryValues)
        {
            SetEnvironmentVariable(item.Key, item.Value);
        }

        CIValues = CIEnvironmentValues.Create(ciDictionaryValues);
    }

    protected void ValidateMetadata(Dictionary<string, Dictionary<string, object>>? metadata, string testCommand)
    {
        metadata.Should().NotBeNull();
        metadata ??= new Dictionary<string, Dictionary<string, object>>();
        metadata.Should().ContainKey("*");
        metadata["*"].Should().ContainKeys(Trace.Tags.RuntimeId, "language", CommonTags.LibraryVersion, "env");

        var jobName = ((CIEnvironmentValues?)CIValues)?.JobName;
        var valueKey = string.IsNullOrEmpty(jobName) ? testCommand : $"{jobName}-{testCommand}";

        Expression<Func<KeyValuePair<string, object>, bool>> selector =
            kv =>
                kv.Key == "test_session.name" &&
                kv.Value.ToString() == valueKey;

        metadata.Should().ContainKey(SpanTypes.Test);
        metadata[SpanTypes.Test].Should().Contain(selector);

        metadata.Should().ContainKey(SpanTypes.TestSuite);
        metadata[SpanTypes.TestSuite].Should().Contain(selector);

        metadata.Should().ContainKey(SpanTypes.TestModule);
        metadata[SpanTypes.TestModule].Should().Contain(selector);

        metadata.Should().ContainKey(SpanTypes.TestSession);
        metadata[SpanTypes.TestSession].Should().Contain(selector);
    }

    protected void InjectSession(
        out ulong sessionId,
        out string sessionCommand,
        out string sessionWorkingDirectory,
        out string gitRepositoryUrl,
        out string gitBranch,
        out string gitCommitSha)
    {
        // Inject session
        sessionId = RandomIdGenerator.Shared.NextSpanId();
        sessionCommand = "test command";
        sessionWorkingDirectory = "C:\\evp_demo\\working_directory";
        SetEnvironmentVariable(HttpHeaderNames.TraceId.Replace(".", "_").Replace("-", "_").ToUpperInvariant(), sessionId.ToString(CultureInfo.InvariantCulture));
        SetEnvironmentVariable(HttpHeaderNames.ParentId.Replace(".", "_").Replace("-", "_").ToUpperInvariant(), sessionId.ToString(CultureInfo.InvariantCulture));
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, sessionCommand);
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, sessionWorkingDirectory);

        gitRepositoryUrl = "git@github.com:DataDog/dd-trace-dotnet.git";
        gitBranch = "main";
        gitCommitSha = "3245605c3d1edc67226d725799ee969c71f7632b";
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.GitRepositoryUrl, gitRepositoryUrl);
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.GitBranch, gitBranch);
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.GitCommitSha, gitCommitSha);

        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Logs, "1");
    }

    protected virtual async Task ExecuteTestAsync(string packageVersion, string evpVersionToRemove, bool expectedGzip, TestScenario testScenario)
    {
        var executionData = new ExecutionData();

        // Inject session
        InjectSession(
            out var sessionId,
            out var sessionCommand,
            out var sessionWorkingDirectory,
            out var gitRepositoryUrl,
            out var gitBranch,
            out var gitCommitSha);

        try
        {
            using var logsIntake = new MockLogsIntakeForCiVisibility();
            EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.XUnit), nameof(XUnitTests));
            using var agent = EnvironmentHelper.GetMockAgent(useStatsD: true);
            agent.Configuration.Endpoints = agent.Configuration.Endpoints.Where(e => !e.Contains(evpVersionToRemove)).ToArray();

            const string correlationId = "2e8a36bda770b683345957cc6c15baf9";
            agent.EventPlatformProxyPayloadReceived += (sender, e) =>
            {
                if (e.Value.PathAndQuery.EndsWith("api/v2/libraries/tests/services/setting"))
                {
                    e.Value.Response = new MockTracerResponse(testScenario.MockData.SettingsJson, 200);
                    return;
                }

                if (e.Value.PathAndQuery.EndsWith("api/v2/ci/libraries/tests"))
                {
                    e.Value.Response = string.IsNullOrEmpty(testScenario.MockData.TestsJson) ? new MockTracerResponse(string.Empty, 404) : new MockTracerResponse(testScenario.MockData.TestsJson, 200);
                    return;
                }

                if (e.Value.PathAndQuery.EndsWith("api/v2/test/libraries/test-management/tests"))
                {
                    e.Value.Response = string.IsNullOrEmpty(testScenario.MockData.TestManagementTestsJson) ? new MockTracerResponse(string.Empty, 404) : new MockTracerResponse(testScenario.MockData.TestManagementTestsJson, 200);
                    return;
                }

                if (e.Value.PathAndQuery.EndsWith("api/v2/ci/tests/skippable"))
                {
                    e.Value.Response = new MockTracerResponse($"{{\"data\":[],\"meta\":{{\"correlation_id\":\"{correlationId}\"}}}}", 200);
                    return;
                }

                if (e.Value.PathAndQuery.EndsWith("api/v2/citestcycle"))
                {
                    e.Value.Headers["Content-Encoding"].Should().Be(expectedGzip ? "gzip" : null);

                    var payload = JsonConvert.DeserializeObject<MockCIVisibilityProtocol>(e.Value.BodyInJson);
                    if (payload?.Events?.Length > 0)
                    {
                        foreach (var @event in payload.Events)
                        {
                            if (@event.Content.ToString() is { } eventContent)
                            {
                                if (@event.Type == SpanTypes.Test)
                                {
                                    if (JsonConvert.DeserializeObject<MockCIVisibilityTest>(eventContent) is { } test)
                                    {
                                        executionData.Tests.Add(test);
                                    }
                                }
                                else if (@event.Type == SpanTypes.TestSuite)
                                {
                                    if (JsonConvert.DeserializeObject<MockCIVisibilityTestSuite>(eventContent) is { } testSuite)
                                    {
                                        executionData.TestSuites.Add(testSuite);
                                    }
                                }
                                else if (@event.Type == SpanTypes.TestModule)
                                {
                                    if (JsonConvert.DeserializeObject<MockCIVisibilityTestModule>(eventContent) is { } testModule)
                                    {
                                        executionData.TestModules.Add(testModule);
                                    }
                                }
                            }
                        }
                    }
                }
            };

            using var processResult = await RunDotnetTestSampleAndWaitForExit(agent, packageVersion: packageVersion, expectedExitCode: testScenario.ExpectedExitCode, useDotnetExec: testScenario.UseDotnetExec);
            Assert.Equal(testScenario.ExpectedSpans, executionData.Tests.Count);

            // Call the validate action
            testScenario.ValidateAction?.Invoke(in executionData);

            if (testScenario.UseSnapshot)
            {
                // Snapshot testing
                var settings = VerifyHelper.GetCIVisibilitySpanVerifierSettings();
                settings.UseTextForParameters(testScenario.FriendlyName);
                settings.DisableRequireUniquePrefix();
                if (testScenario.TypeName is not null)
                {
                    settings.UseTypeName(testScenario.TypeName);
                }

                await Verifier.Verify(
                    executionData.Tests
                                 .OrderBy(s => s.Resource)
                                 .ThenBy(s => GetValueOrDefault(s.Meta, TestTags.Name))
                                 .ThenBy(s => GetValueOrDefault(s.Meta, TestTags.Parameters))
                                 .ThenBy(s => GetValueOrDefault(s.Meta, TestTags.TestIsNew))
                                 .ThenBy(s => GetValueOrDefault(s.Meta, TestTags.TestIsRetry))
                                 .ThenBy(s => GetValueOrDefault(s.Meta, TestTags.TestAttemptToFixPassed))
                                 .ThenBy(s => GetValueOrDefault(s.Meta, TestTags.TestHasFailedAllRetries))
                                 .ThenBy(s => GetValueOrDefault(s.Meta, EarlyFlakeDetectionTags.AbortReason)),
                    settings);
            }
        }
        catch
        {
            Output.WriteLine("Framework Version: " + new Version(FrameworkDescription.Instance.ProductVersion));
            if (!string.IsNullOrWhiteSpace(packageVersion))
            {
                Output.WriteLine("Package Version: " + new Version(packageVersion));
            }

            WriteSpans(executionData.Tests);
            throw;
        }
    }

    private static TValue? GetValueOrDefault<TKey, TValue>(IDictionary<TKey, TValue> dictionary, TKey key)
        where TKey : notnull => dictionary.TryGetValue(key, out var value) ? value : default;

    public readonly struct MockData
    {
        public readonly string SettingsJson;
        public readonly string TestsJson;
        public readonly string TestManagementTestsJson;

        public MockData(string settingsJson, string testsJson, string testManagementTestsJson)
        {
            SettingsJson = settingsJson;
            TestsJson = testsJson;
            TestManagementTestsJson = testManagementTestsJson;
        }

        public override string ToString()
        {
            return $"SettingsJson: {SettingsJson}, TestsJson: {TestsJson}, TestManagementTestsJson: {TestManagementTestsJson}";
        }
    }

    public readonly struct ExecutionData
    {
        public readonly List<MockCIVisibilityTest> Tests;
        public readonly List<MockCIVisibilityTestSuite> TestSuites;
        public readonly List<MockCIVisibilityTestModule> TestModules;

        public ExecutionData()
        {
            Tests = new List<MockCIVisibilityTest>();
            TestSuites = new List<MockCIVisibilityTestSuite>();
            TestModules = new List<MockCIVisibilityTestModule>();
        }

        public delegate void ValidateDelegate(in ExecutionData data);
    }

    public readonly struct TestScenario
    {
        public readonly string? TypeName;
        public readonly string FriendlyName;
        public readonly MockData MockData;
        public readonly int ExpectedExitCode;
        public readonly int ExpectedSpans;
        public readonly bool UseSnapshot;
        public readonly bool UseDotnetExec;
        public readonly ExecutionData.ValidateDelegate ValidateAction;

        public TestScenario(string? typeName, string friendlyName, MockData mockData, int expectedExitCode, int expectedSpans, bool useSnapshot, ExecutionData.ValidateDelegate validateAction, bool useDotnetExec = true)
        {
            TypeName = typeName;
            FriendlyName = friendlyName;
            MockData = mockData;
            ExpectedExitCode = expectedExitCode;
            ExpectedSpans = expectedSpans;
            UseSnapshot = useSnapshot;
            UseDotnetExec = useDotnetExec;
            ValidateAction = validateAction;
        }
    }

    protected class MockLogsIntakeForCiVisibility : MockLogsIntake<MockLogsIntakeForCiVisibility.Log>
    {
        public class Log
        {
            [JsonProperty("ddsource")]
            public string? Source { get; set; }

            [JsonProperty("hostname")]
            public string? Hostname { get; set; }

            [JsonProperty("timestamp")]
            public long Timestamp { get; set; }

            [JsonProperty("message")]
            public string? Message { get; set; }

            [JsonProperty("status")]
            public string? Status { get; set; }

            [JsonProperty("service")]
            public string? Service { get; set; }

            [JsonProperty("dd.trace_id")]
            public string? TraceId { get; set; }

            [JsonProperty(TestTags.Suite)]
            public string? TestSuite { get; set; }

            [JsonProperty(TestTags.Name)]
            public string? TestName { get; set; }

            [JsonProperty(TestTags.Bundle)]
            public string? TestBundle { get; set; }

            [JsonProperty("ddtags")]
            public string? Tags { get; set; }
        }
    }
}
