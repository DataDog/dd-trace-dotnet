// <copyright file="XUnitImpactedTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    [UsesVerify]
    public class XUnitImpactedTests : TestingFrameworkImpactedTests
    {
        private const int ExpectedSpanCount = 41;
        private const string IsModifiedTag = "test.is_modified";

        public XUnitImpactedTests(ITestOutputHelper output)
            : base("XUnitTests", output)
        {
            SetServiceName("xunit-tests");
            SetServiceVersion("1.0.0");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.XUnit), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public Task BaseShaFromPr(string packageVersion)
        {
            InjectGitHubActionsSession();
            return SubmitTests(packageVersion, 2, TestIsModified);
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.XUnit), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public Task BaseShaFromBackend(string packageVersion)
        {
            InjectGitHubActionsSession(false);
            return SubmitTests(packageVersion, 2, TestIsModified);
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.XUnit), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public Task FilesFromBackend(string packageVersion)
        {
            InjectGitHubActionsSession(false);
            Action<MockTracerAgent.EvpProxyPayload, List<MockCIVisibilityTest>> agentRequestProcessor = (request, receivedTests) =>
            {
                if (request.PathAndQuery.EndsWith("ci/tests/diffs"))
                {
                    request.Response = new MockTracerResponse(GetDiffFilesJson(false), 200);
                    return;
                }

                ProcessAgentRequest(request, receivedTests);
            };
            return SubmitTests(packageVersion, 12, TestIsModified, agentRequestProcessor);
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.XUnit), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public Task DisabledByEnvVar(string packageVersion)
        {
            InjectGitHubActionsSession(true, false);
            return SubmitTests(packageVersion, 0, TestIsModified);
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.XUnit), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public Task EnabledBySettings(string packageVersion)
        {
            Skip.If(EnvironmentHelper.IsAlpine(), "This test is currently flaky in alpine due to a Detached Head status. An issue has been opened to handle the situation. Meanwhile we are skipping it.");

            InjectGitHubActionsSession(true, null);
            return SubmitTests(packageVersion, 2, TestIsModified);
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.XUnit), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public async Task GitBranchBasedImpactDetection(string packageVersion)
        {
            // Check for Git availability
            Skip.IfNot(gitAvailable, "Git not available or not properly configured in current environment");

            var testBranchName = $"test-impact-detection-{Guid.NewGuid():N}";
            var originalBranch = string.Empty;

            try
            {
                // Get the current branch name
                var currentBranchOutput = RunGitCommand("branch --show-current");
                Skip.IfNot(currentBranchOutput.ExitCode == 0, "Failed to get current branch");
                originalBranch = currentBranchOutput.Output.Trim();

                // Create and checkout a new test branch
                var createBranchOutput = RunGitCommand($"checkout -b {testBranchName}");
                Skip.IfNot(createBranchOutput.ExitCode == 0, $"Failed to create test branch: {createBranchOutput.Error}");

                // Modify the test file
                ModifyFile();

                // Stage and commit the changes
                var addOutput = RunGitCommand($"add {GetTestFile()}");
                Skip.IfNot(addOutput.ExitCode == 0, $"Failed to stage changes: {addOutput.Error}");

                var commitOutput = RunGitCommand($"commit -m \"Test modifications for impact detection test\"");
                Skip.IfNot(commitOutput.ExitCode == 0, $"Failed to commit changes: {commitOutput.Error}");

                // Enable impact detection
                SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ImpactedTestsDetectionEnabled, "True");
                SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
                SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Logs, "1");

                // Run the test submission without GitHub Actions injection
                await SubmitTestsWithGitBranch(packageVersion, 2, TestIsModified);
            }
            finally
            {
                try
                {
                    // Restore the file
                    RestoreFile();

                    // Switch back to original branch
                    if (!string.IsNullOrEmpty(originalBranch))
                    {
                        RunGitCommand($"checkout {originalBranch}");
                    }

                    // Delete the test branch
                    if (!string.IsNullOrEmpty(testBranchName))
                    {
                        RunGitCommand($"branch -D {testBranchName}");
                    }
                }
                catch (Exception ex)
                {
                    Output.WriteLine($"Cleanup error: {ex.Message}");
                }
            }
        }

        private async Task SubmitTestsWithGitBranch(string packageVersion, int expectedTests, Func<MockCIVisibilityTest, bool> testFilter = null, Action<MockTracerAgent.EvpProxyPayload, List<MockCIVisibilityTest>> agentRequestProcessor = null)
        {
            var tests = new List<MockCIVisibilityTest>();
            using var agent = GetAgent(tests, agentRequestProcessor);

            using var processResult = await RunDotnetTestSampleAndWaitForExit(agent, packageVersion: packageVersion, expectedExitCode: 1);
            var deadline = DateTime.UtcNow.AddMilliseconds(5000);
            testFilter ??= _ => true;

            List<MockCIVisibilityTest> filteredTests = tests;
            while (DateTime.UtcNow < deadline)
            {
                filteredTests = tests.Where(testFilter).ToList();
                if (tests.Count() >= ExpectedTestCount)
                {
                    break;
                }

                Thread.Sleep(500);
            }

            // Sort and aggregate
            var results = filteredTests.Select(t => t.Resource).Distinct().OrderBy(t => t).ToList();

            tests.Count().Should().BeGreaterOrEqualTo(ExpectedTestCount, "Expected test count not met");
            results.Count().Should().Be(expectedTests, "Expected filtered test count not met");

            // Additional validation: ensure only modified tests have the is_modified tag
            var modifiedTests = tests.Where(TestIsModified).ToList();
            var nonModifiedTests = tests.Where(t => !TestIsModified(t)).ToList();

            modifiedTests.Count.Should().Be(expectedTests, "Expected number of modified tests not met");
            nonModifiedTests.Count.Should().Be(tests.Count - expectedTests, "Unexpected tests marked as modified");

            Output.WriteLine($"Total tests: {tests.Count}, Modified tests: {modifiedTests.Count}, Non-modified tests: {nonModifiedTests.Count}");
        }

        private static bool TestIsModified(MockCIVisibilityTest t) => t.Meta.ContainsKey(IsModifiedTag) && t.Meta[IsModifiedTag] == "true";
    }
} 