// <copyright file="TestingFrameworkImpactedTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
    protected const int ExpectedTestCount = 16;
    protected string baseSha = string.Empty;
    protected string repositoryRoot = string.Empty;
    protected string repo = string.Empty;
    protected string branch = string.Empty;
    protected bool gitAvailable = false;
#pragma warning restore SA1401 // FieldsMustBePrivate

    public TestingFrameworkImpactedTests(string sampleAppName, ITestOutputHelper output)
        : base(sampleAppName, output)
    {
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
                          "tracer/test/test-applications/integrations/Samples.XUnitTests/TestSuite.cs"
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

            using var processResult = await RunDotnetTestSampleAndWaitForExit(agent, packageVersion: packageVersion, expectedExitCode: 1);
            var deadline = DateTime.UtcNow.AddMilliseconds(5000);
            testFilter ??= _ => true; // t => t.Meta.ContainsKey("is_modified")

            List<MockCIVisibilityTest> filteredTests = tests;
            while (DateTime.UtcNow < deadline)
            {
                filteredTests = tests.Where(testFilter).ToList();
                if (tests.Count() >= ExpectedTestCount)
                {
                    break;
                }

                await Task.Delay(500);
            }

            // Sort and aggregate
            var results = filteredTests.Select(t => t.Resource).Distinct().OrderBy(t => t).ToList();

            tests.Count().Should().BeGreaterOrEqualTo(ExpectedTestCount, "Expected test count not met");
            results.Count().Should().Be(expectedTests, "Expected filtered test count not met");
        }
        finally
        {
            RestoreFile();
        }
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
        return Path.Combine(repositoryRoot, "tracer/test/test-applications/integrations/Samples.XUnitTests/TestSuite.cs");
    }

    protected void ModifyFile()
    {
        var path = GetTestFile();
        var lines = File.ReadAllLines(path).ToList();
        lines.Insert(33, ModifiedLine);
        lines.Insert(63, ModifiedLine);
        lines.Insert(64, ModifiedLine);
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
