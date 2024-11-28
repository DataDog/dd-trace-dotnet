// <copyright file="XUnitImpactedTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    [UsesVerify]
    public class XUnitImpactedTests : TestingFrameworkTest
    {
        private const int ExpectedSpanCount = 16;
        private const string GitHubSha = "f8c18a2ed1f91447fc3da6adc655f4a7a91bf99c";

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
        public virtual async Task SubmitTraces(string packageVersion)
        {
            // TODO :  Get test source file path, modify, launch test and restore

            SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
            SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Logs, "1");
            SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ImpactedTestsDetection, "True");

            using var agent = EnvironmentHelper.GetMockAgent();

            // We remove the evp_proxy endpoint to force the APM protocol compatibility
            agent.Configuration.Endpoints = agent.Configuration.Endpoints.Where(e => !e.Contains("evp_proxy/v2") && !e.Contains("evp_proxy/v4")).ToArray();
            using var processResult = await RunDotnetTestSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = agent.WaitForSpans(ExpectedSpanCount)
                         .Where(s => !(s.Tags.TryGetValue(Tags.InstrumentationName, out var sValue) && sValue == "HttpMessageHandler"))
                         .ToList();
            var spansCopy = JsonConvert.DeserializeObject<List<MockSpan>>(JsonConvert.SerializeObject(spans));

            // Snapshot testing
            var settings = VerifyHelper.GetCIVisibilitySpanVerifierSettings("all");
            settings.DisableRequireUniquePrefix();
            settings.UseTypeName(nameof(XUnitImpactedTests));
            await Verifier.Verify(spansCopy.OrderBy(s => s.Resource).ThenBy(s => s.Tags.GetValueOrDefault(TestTags.Parameters)), settings);
        }

        protected override Dictionary<string, string> DefineCIEnvironmentValues(Dictionary<string, string> values)
        {
            // Base sets Azure CI values. Take those we can reuse for Git Hub

            var buildDir = values[CIEnvironmentValues.Constants.AzureBuildSourcesDirectory];
            var repo = values[CIEnvironmentValues.Constants.AzureBuildRepositoryUri];
            var branch = values[CIEnvironmentValues.Constants.AzureBuildSourceBranch];

            // Reset all the envVars for spawned process (override possibly existing env vars)
            var res = new Dictionary<string, string>();
            foreach (var field in typeof(CIEnvironmentValues.Constants).GetFields())
            {
                var fieldValue = field.GetValue(null) as string;
                res[fieldValue] = string.Empty;
            }

            // Set relevant GitHub variables
            res[CIEnvironmentValues.Constants.GitHubRepository] = repo;
            res[CIEnvironmentValues.Constants.GitHubBaseRef] = branch;
            res[CIEnvironmentValues.Constants.GitHubWorkspace] = buildDir;
            res[CIEnvironmentValues.Constants.GitHubSha] = GitHubSha;
            res[CIEnvironmentValues.Constants.GitHubSha] = GitHubSha;
            res[CIEnvironmentValues.Constants.GitHubEventPath] = GetEventJsonFile();

            return res;

            static string GetEventJsonFile()
            {
                string content = $$"""
                {
                  "pull_request": {
                    "head": {
                      "sha": "{{GitHubSha}}"
                    },
                    "base": {
                      "sha": "56bef05720c791ba6246a95c36bd0ae80a3d3441"
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
