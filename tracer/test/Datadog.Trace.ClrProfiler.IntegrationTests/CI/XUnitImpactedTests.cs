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
using Datadog.Trace.TestHelpers.Ci;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    [UsesVerify]
    public class XUnitImpactedTests : TestingFrameworkImpactedTests
    {
        private const int ExpectedSpanCount = 16;

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
            return SubmitTests(packageVersion, $"baseShaFromPr", 12, (t) => t.Meta.ContainsKey("test.is_modified") && t.Meta["test.is_modified"] == "true");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.XUnit), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public Task BaseShaFromBackend(string packageVersion)
        {
            InjectGitHubActionsSession(false);
            return SubmitTests(packageVersion, $"baseShaFromPr", 41, (t) => t.Meta.ContainsKey("test.is_modified") && t.Meta["test.is_modified"] == "true");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.XUnit), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public Task DisabledByConfig(string packageVersion)
        {
            InjectGitHubActionsSession();
            var tests = new List<MockCIVisibilityTest>();
            bool configDelivered = false;

            Action<MockTracerAgent.EvpProxyPayload, List<MockCIVisibilityTest>> agentRequestProcessor = (request, receivedTests) =>
            {
                if (request.PathAndQuery.EndsWith("api/v2/libraries/tests/services/setting"))
                {
                    request.Response = new MockTracerResponse(GetSettingsJson(false), 200);
                    configDelivered = true;
                    return;
                }

                ProcessAgentRequest(request, receivedTests);
            };

            var res = SubmitTests(packageVersion, $"baseShaFromPr", 0, (t) => t.Meta.ContainsKey("is_modified"), agentRequestProcessor);
            configDelivered.Should().BeTrue("Config was not delivered to the agent");
            return res;
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.XUnit), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public Task DisabledByEnvVar(string packageVersion)
        {
            InjectGitHubActionsSession();
            SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ImpactedTestsDetectionEnabled, "False");
            return SubmitTests(packageVersion, $"baseShaFromPr", 0, (t) => t.Meta.ContainsKey("is_modified"));
        }

        private void InjectGitHubActionsSession(bool setupPr = true)
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

        /*
                [SkippableTheory]
                [MemberData(nameof(PackageVersions.XUnit), MemberType = typeof(PackageVersions))]
                [Trait("Category", "EndToEnd")]
                [Trait("Category", "TestIntegrations")]
                public Task BaseShaFromBackend(string packageVersion)
                {
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

                    return SubmitTraces(packageVersion, "baseShaFromPr");
                }
        */
    }
}
