// <copyright file="TestingFrameworkImpactedTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Ci.CiEnvironment;
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

[UsesVerify]
public abstract class TestingFrameworkImpactedTests : TestingFrameworkTest
{
#pragma warning disable SA1401 // FieldsMustBePrivate
    protected const string ModifiedLine = "// Modified by TestingFrameworkImpactedTests.cs";
    protected string baseSha = string.Empty;
    protected string repositoryRoot = string.Empty;
    protected string repo = string.Empty;
    protected string branch = string.Empty;
    protected bool gitAvailable = false;
#pragma warning restore SA1401 // FieldsMustBePrivate

    private const int DefaultExpectedTestCount = 16;
    private const string DefaultTestFileRelativePath = "tracer/test/test-applications/integrations/Samples.XUnitTests/TestSuite.cs";
    private static readonly string[] DefaultModificationMarkers =
    [
        "_output.WriteLine(\"Test:SimplePassTest\");",
        "public void TraitSkipFromAttributeTest()",
    ];

    private readonly int _expectedTestCount;
    private readonly string[] _modificationMarkers;
    private readonly string _testFileRelativePath;
    private readonly string _testRunnerArguments;
    private readonly bool _useDotnetExec;

    public TestingFrameworkImpactedTests(string sampleAppName, ITestOutputHelper output)
        : this(sampleAppName, DefaultTestFileRelativePath, DefaultExpectedTestCount, useDotnetExec: false, DefaultModificationMarkers, testRunnerArguments: null, output)
    {
    }

    public TestingFrameworkImpactedTests(string sampleAppName, string testFileRelativePath, int expectedTestCount, bool useDotnetExec, string[] modificationMarkers, string testRunnerArguments, ITestOutputHelper output)
        : base(sampleAppName, output)
    {
        _testFileRelativePath = testFileRelativePath;
        _expectedTestCount = expectedTestCount;
        _useDotnetExec = useDotnetExec;
        _modificationMarkers = modificationMarkers;
        _testRunnerArguments = testRunnerArguments;
        InitGit();
        SetCIEnvironmentValues();
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Logs, "1");
    }

    internal ProcessHelpers.CommandOutput RunGitCommand(string arguments)
    {
        try
        {
            var gitOutput = ProcessHelpers.RunCommand(
                new ProcessHelpers.Command(
                    "git",
                    arguments,
                    EnvironmentTools.GetSolutionDirectory(),
                    outputEncoding: Encoding.Default,
                    errorEncoding: Encoding.Default,
                    inputEncoding: Encoding.Default,
                    useWhereIsIfFileNotFound: true),
                null);

            if (gitOutput is null || (gitOutput.ExitCode != 0 && gitOutput.Error is not { Length: > 0 }))
            {
                return new ProcessHelpers.CommandOutput(null, "git command returned null output", -1, false);
            }

            return gitOutput;
        }
        catch (Exception err)
        {
            return new ProcessHelpers.CommandOutput(null, err.ToString(), -1, false);
        }
    }

    protected string GetSettingsJson(bool enabled = false)
    {
        var enabledValue = enabled ? "true" : "false";
        return $$"""
                 {
                     "data":
                     {
                         "id":"511938a3f19c12f8bb5e5caa695ca24f4563de3f",
                         "type":"ci_app_tracers_test_service_settings",
                         "attributes":
                         {
                             "code_coverage":false,
                             "flaky_test_retries_enabled":true,
                             "itr_enabled":false,
                             "require_git":false,
                             "tests_skipping":false,
                             "impacted_tests_enabled":{{enabledValue}},
                             "early_flake_detection":
                             {
                                 "enabled":false,
                                 "slow_test_retries":{"10s":5,"30s":3,"5m":2,"5s":10},
                                 "faulty_session_threshold":100
                             }
                          }
                      }
                  }
                 """;
    }

    protected string GetDiffFilesJson(bool baseCommit = true)
    {
        var commitValue = baseCommit ? baseSha : string.Empty;
        return $$"""
                 {
                   "data": {
                     "type": "ci_app_tests_diffs_response",
                     "id": "123456",
                     "attributes": {
                       "base_sha": "{{commitValue}}",
                       "files": [
                          "{{_testFileRelativePath}}"
                       ]
                     }
                   }
                 }
                 """;
    }

    protected void ProcessAgentRequest(MockTracerAgent.EvpProxyPayload request, List<MockCIVisibilityTest> receivedTests)
    {
        if (request.PathAndQuery.EndsWith("libraries/tests/services/setting"))
        {
            request.Response = new MockTracerResponse(GetSettingsJson(true), 200);
            return;
        }

        if (request.PathAndQuery.EndsWith("ci/tests/diffs"))
        {
            request.Response = new MockTracerResponse(GetDiffFilesJson(true), 200);
            return;
        }

        if (request.PathAndQuery.EndsWith("api/v2/citestcycle"))
        {
            var payload = JsonConvert.DeserializeObject<MockCIVisibilityProtocol>(request.BodyInJson);
            if (payload.Events?.Length > 0)
            {
                foreach (var @event in payload.Events)
                {
                    if (@event.Content.ToString() is { } eventContent)
                    {
                        if (@event.Type == SpanTypes.Test)
                        {
                            receivedTests.Add(JsonConvert.DeserializeObject<MockCIVisibilityTest>(eventContent));
                        }
                    }
                }
            }
        }
    }

    protected async Task SubmitTests(string packageVersion, int expectedTests, Func<MockCIVisibilityTest, bool> testFilter = null, Action<MockTracerAgent.EvpProxyPayload, List<MockCIVisibilityTest>> agentRequestProcessor = null)
    {
        try
        {
            ModifyFile();

            var tests = new List<MockCIVisibilityTest>();
            using var agent = GetAgent(tests, agentRequestProcessor);

            using var processResult = await RunDotnetTestSampleAndWaitForExit(agent, arguments: _testRunnerArguments, packageVersion: packageVersion, expectedExitCode: 1, useDotnetExec: _useDotnetExec);
            testFilter ??= static _ => true;
            var filteredTests = tests.Where(testFilter).ToList();

            // Sort and aggregate
            var results = filteredTests.Select(t => t.Resource).Distinct().OrderBy(t => t).ToList();

            tests.Count.Should().BeGreaterOrEqualTo(_expectedTestCount, "Expected test count not met");
            results.Count().Should().Be(expectedTests, "Expected filtered test count not met");
        }
        finally
        {
            RestoreFile();
        }
    }

    protected async Task SubmitTestsUsingGitBranch(string packageVersion, int expectedTests, Func<MockCIVisibilityTest, bool> testFilter)
    {
        Skip.IfNot(gitAvailable, "Git not available or not properly configured in current environment");

        var testBranchName = $"test-impact-detection-{Guid.NewGuid():N}";
        var currentBranchOutput = RunGitCommand("branch --show-current");
        currentBranchOutput.ExitCode.Should().Be(0, "Failed to get current branch");
        var originalBranch = currentBranchOutput.Output.Trim();
        var originalHeadOutput = RunGitCommand("rev-parse --verify HEAD");
        originalHeadOutput.ExitCode.Should().Be(0, "Failed to get current HEAD");
        var originalHead = originalHeadOutput.Output.Trim();
        var statusOutput = RunGitCommand("status --porcelain");
        statusOutput.ExitCode.Should().Be(0, "Failed to get worktree status");
        Skip.IfNot(string.IsNullOrWhiteSpace(statusOutput.Output), "Git branch impact detection requires a clean working tree");
        var testBranchCreated = false;

        try
        {
            var createBranchOutput = RunGitCommand($"checkout -b {testBranchName}");
            testBranchCreated = createBranchOutput.ExitCode == 0;
            createBranchOutput.ExitCode.Should().Be(0, $"Failed to create test branch: {createBranchOutput.Error}");

            ModifyFile();
            var addOutput = RunGitCommand($"add {GetTestFile()}");
            addOutput.ExitCode.Should().Be(0, $"Failed to stage changes: {addOutput.Error}");

            var commitOutput = RunGitCommand("commit -m \"Test modifications for impact detection test\"");
            commitOutput.ExitCode.Should().Be(0, $"Failed to commit changes: {commitOutput.Error}");

            SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ImpactedTestsDetectionEnabled, "True");
            SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
            SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Logs, "1");
            SetEnvironmentVariable(PlatformKeys.Ci.Azure.SystemPullRequestSourceBranch, testBranchName);
            SetEnvironmentVariable(PlatformKeys.Ci.Azure.BuildSourceBranch, testBranchName);
            SetEnvironmentVariable(PlatformKeys.Ci.Azure.BuildSourceBranchName, testBranchName);

            await SubmitTestsWithGitBranch(packageVersion, expectedTests, testFilter);
        }
        finally
        {
            var cleanupFailures = new List<string>();
            var restoreOutput = RunGitCommand($"restore --source {originalHead} --staged --worktree -- {GetTestFile()}");
            if (restoreOutput.ExitCode != 0)
            {
                cleanupFailures.Add($"Failed to restore the impacted-test file: {restoreOutput.Error}");
            }

            ProcessHelpers.CommandOutput checkoutOutput;
            if (!string.IsNullOrEmpty(originalBranch))
            {
                checkoutOutput = RunGitCommand($"checkout {originalBranch}");
            }
            else
            {
                checkoutOutput = RunGitCommand($"checkout --detach {originalHead}");
            }

            if (checkoutOutput.ExitCode != 0)
            {
                cleanupFailures.Add($"Failed to restore the original checkout: {checkoutOutput.Error}");
            }

            if (testBranchCreated)
            {
                var deleteBranchOutput = RunGitCommand($"branch -D {testBranchName}");
                if (deleteBranchOutput.ExitCode != 0)
                {
                    cleanupFailures.Add($"Failed to delete temporary branch {testBranchName}: {deleteBranchOutput.Error}");
                }
            }

            cleanupFailures.Should().BeEmpty("the branch-based impacted test must restore its Git state");
        }
    }

    protected async Task SubmitTestsWithGitBranch(string packageVersion, int expectedTests, Func<MockCIVisibilityTest, bool> testFilter, Action<MockTracerAgent.EvpProxyPayload, List<MockCIVisibilityTest>> agentRequestProcessor = null)
    {
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestOptimizationRunId, Guid.NewGuid().ToString("n"));

        var tests = new List<MockCIVisibilityTest>();
        using var agent = GetAgent(tests, agentRequestProcessor);
        using var processResult = await RunDotnetTestSampleAndWaitForExit(agent, arguments: _testRunnerArguments, packageVersion: packageVersion, expectedExitCode: 1, useDotnetExec: _useDotnetExec);
        var filteredTests = tests.Where(testFilter).ToList();

        var results = filteredTests.Select(test => test.Resource).Distinct().OrderBy(resource => resource).ToList();
        tests.Count.Should().BeGreaterOrEqualTo(_expectedTestCount, "Expected test count not met");
        results.Count.Should().Be(expectedTests, "Expected filtered test count not met");

        var nonModifiedTests = tests.Where(test => !testFilter(test)).ToList();
        filteredTests.Count.Should().Be(expectedTests, "Expected number of modified tests not met");
        nonModifiedTests.Count.Should().Be(tests.Count - expectedTests, "Unexpected tests marked as modified");
    }

    protected override Dictionary<string, string> DefineCIEnvironmentValues(Dictionary<string, string> values)
    {
        // Base sets Azure CI values. Take those we can reuse for GitHub
        repo = values[PlatformKeys.Ci.Azure.BuildRepositoryUri];

        return values;
    }

    protected void InjectGitHubActionsSession(bool setupPr = true, bool? enabled = true)
    {
        // Check for GIT availability
        Skip.IfNot(gitAvailable, "Git not available or not properly configured in current environment");

        // Reset all the envVars for the spawned process (override possibly existing env vars)
        var allFields = new List<System.Reflection.FieldInfo>();
        allFields.AddRange(GetAllFieldsRecursive(typeof(PlatformKeys.Ci)));
        allFields.AddRange(GetAllFieldsRecursive(typeof(ConfigurationKeys.CIVisibility)));

        foreach (var field in allFields)
        {
            var fieldName = field.GetValue(null) as string;
            SetEnvironmentVariable(fieldName, string.Empty);
        }

        // Set relevant GitHub variables
        SetEnvironmentVariable(PlatformKeys.Ci.GitHub.Sha, baseSha);
        SetEnvironmentVariable(PlatformKeys.Ci.GitHub.Repository, repo);
        SetEnvironmentVariable(PlatformKeys.Ci.GitHub.BaseRef, branch);
        SetEnvironmentVariable(PlatformKeys.Ci.GitHub.Workspace, repositoryRoot);
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
        if (setupPr)
        {
            SetEnvironmentVariable(PlatformKeys.Ci.GitHub.EventPath, GetEventJsonFile());
        }

        if (enabled is not null)
        {
            SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ImpactedTestsDetectionEnabled, enabled.Value ? "True" : "False");
        }

        string GetEventJsonFile()
        {
            string content = $$"""
                               {
                                 "pull_request": {
                                   "base": {
                                     "sha": "{{baseSha}}"
                                   }
                                 }
                               }
                               """;
            var tmpFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_event.json");
            File.WriteAllText(tmpFileName, content);
            return tmpFileName;
        }
    }

    protected MockTracerAgent GetAgent(List<MockCIVisibilityTest> receivedTests, Action<MockTracerAgent.EvpProxyPayload, List<MockCIVisibilityTest>> processRequest = null)
    {
        var agent = EnvironmentHelper.GetMockAgent();
        agent.EventPlatformProxyPayloadReceived += (sender, e) =>
        {
            if (processRequest != null)
            {
                processRequest(e.Value, receivedTests);
                return;
            }

            ProcessAgentRequest(e.Value, receivedTests);
        };

        return agent;
    }

    protected string GetTestFile()
    {
        return Path.Combine(repositoryRoot, _testFileRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    protected void ModifyFile()
    {
        var path = GetTestFile();
        var lines = File.ReadAllLines(path).ToList();
        foreach (var marker in _modificationMarkers)
        {
            var markerIndex = lines.FindIndex(line => line.IndexOf(marker, StringComparison.Ordinal) >= 0);
            if (markerIndex < 0)
            {
                throw new InvalidOperationException($"Unable to find impacted-test modification marker '{marker}' in '{path}'.");
            }

            lines.Insert(markerIndex + 1, ModifiedLine);
        }

        File.WriteAllLines(path, lines);
    }

    protected void RestoreFile()
    {
        var path = GetTestFile();
        var lines = File.ReadAllLines(path).Where(l => l != ModifiedLine).ToList();
        File.WriteAllLines(path, lines);
    }

    private static IEnumerable<System.Reflection.FieldInfo> GetAllFieldsRecursive(Type type)
    {
        var fields = new List<System.Reflection.FieldInfo>();
        // Get fields from the current type
        fields.AddRange(type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static));

        // Get fields from nested types (internal classes)
        foreach (var nestedType in type.GetNestedTypes(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
        {
            fields.AddRange(GetAllFieldsRecursive(nestedType));
        }

        return fields;
    }

    private void InitGit()
    {
        // Check git availability
        var output = RunGitCommand("branch --show-current");
        if (output.ExitCode < 0)
        {
            // Try to fix the git path
            RunGitCommand("config --global --add safe.directory '*'");
            output = RunGitCommand("branch --show-current");
        }

        if (output.ExitCode == 0)
        {
            // Retrieve branch name
            branch = output.Output.Trim();
        }

        if (output.ExitCode == 0)
        {
            // Retrieve last commit
            output = RunGitCommand("rev-parse --verify HEAD");
            if (output.ExitCode == 0)
            {
                baseSha = output.Output.Trim();
                if (string.IsNullOrEmpty(branch))
                {
                    branch = $"auto:git-detached-head";
                }
            }
        }

        if (output.ExitCode == 0)
        {
            // Retrieve WS root directory
            output = RunGitCommand("rev-parse --show-toplevel");
            if (output.ExitCode == 0)
            {
                gitAvailable = true;
                repositoryRoot = output.Output.Trim();
                Output.WriteLine($"Git available. Repository: {repositoryRoot} Branch: {branch} Sha: {baseSha}");
            }
        }

        if (output.ExitCode < 0)
        {
            Output.WriteLine($"Git NOT available. ExitCode: {output.ExitCode} Error: {output.Error}");
        }
    }
}
