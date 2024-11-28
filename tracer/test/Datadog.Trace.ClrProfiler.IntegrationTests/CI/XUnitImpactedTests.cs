// <copyright file="XUnitImpactedTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    [UsesVerify]
    public class XUnitImpactedTests : TestingFrameworkImpactedTests
    {
        private const int ExpectedSpanCount = 41;

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
            return SubmitTests(packageVersion, $"baseShaFromPr", 2, (t) => t.Meta.ContainsKey("test.is_modified") && t.Meta["test.is_modified"] == "true");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.XUnit), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public Task BaseShaFromBackend(string packageVersion)
        {
            InjectGitHubActionsSession(false);
            return SubmitTests(packageVersion, $"baseShaFromPr", 2, (t) => t.Meta.ContainsKey("test.is_modified") && t.Meta["test.is_modified"] == "true");
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
            return SubmitTests(packageVersion, $"baseShaFromPr", 12, (t) => t.Meta.ContainsKey("test.is_modified") && t.Meta["test.is_modified"] == "true", agentRequestProcessor);
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.XUnit), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public Task Disabled(string packageVersion)
        {
            InjectGitHubActionsSession(true, false);
            return SubmitTests(packageVersion, $"baseShaFromPr", 0, (t) => t.Meta.ContainsKey("is_modified"));
        }

        private void InjectGitHubActionsSession(bool setupPr = true, bool enabled = true)
        {
            // Reset all the envVars for spawned process (override possibly existing env vars)
            foreach (var field in typeof(CIEnvironmentValues.Constants).GetFields())
            {
                var fieldName = field.GetValue(null) as string;
                SetEnvironmentVariable(fieldName, string.Empty);
            }

            // Set relevant GitHub variables
            SetEnvironmentVariable(CIEnvironmentValues.Constants.GitHubRepository, repo);
            SetEnvironmentVariable(CIEnvironmentValues.Constants.GitHubBaseRef, branch);
            SetEnvironmentVariable(CIEnvironmentValues.Constants.GitHubWorkspace, buildDir);
            SetEnvironmentVariable(CIEnvironmentValues.Constants.GitHubSha, GitHubSha);
            if (setupPr)
            {
                SetEnvironmentVariable(CIEnvironmentValues.Constants.GitHubEventPath, GetEventJsonFile());
            }

            SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ImpactedTestsDetectionEnabled, enabled ? "True" : "False");

            static string GetEventJsonFile()
            {
                string content = $$"""
                {
                  "pull_request": {
                    "head": {
                      "sha": "{{GitHubSha}}"
                    },
                    "base": {
                      "sha": "{{GitHubBaseSha}}"
                    }
                  }
                }
                """;
                var tmpFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_event.json");
                File.WriteAllText(tmpFileName, content);
                return tmpFileName;
            }
        }
    }
}
