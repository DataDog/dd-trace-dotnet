// <copyright file="TestingFrameworkImpactedTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using VerifyXunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    [UsesVerify]
    public abstract class TestingFrameworkImpactedTests : TestingFrameworkTest
    {
#pragma warning disable SA1401 // FieldsMustBePrivate
        protected const int ExpectedTestCount = 16;
        protected const string GitHubSha = "c7fd869e31de6b621750c7542822c5001d06e421";
        protected const string GitHubBaseSha = "340fa40ce6b5c6c8c45b6a07cfa90e84718f1ab6";
        protected string buildDir = string.Empty;
        protected string repo = string.Empty;
        protected string branch = string.Empty;
#pragma warning restore SA1401 // FieldsMustBePrivate

        public TestingFrameworkImpactedTests(string sampleAppName, ITestOutputHelper output)
        : base(sampleAppName, output)
        {
            SetCIEnvironmentValues();
            SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
            SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Logs, "1");
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
            var commitValue = baseCommit ? GitHubBaseSha : string.Empty;
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

        protected async Task SubmitTests(string packageVersion, string scenario, int expectedTests, Func<MockCIVisibilityTest, bool> testFilter = null, Action<MockTracerAgent.EvpProxyPayload, List<MockCIVisibilityTest>> agentRequestProcessor = null)
        {
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

                Thread.Sleep(500);
            }

            // Sort and aggregate
            var results = filteredTests.Select(t => t.Resource).Distinct().OrderBy(t => t).ToList();

            tests.Count().Should().BeGreaterOrEqualTo(ExpectedTestCount, "Expected test count not met");
            results.Count().Should().Be(expectedTests, "Expected filtered test count not met");
        }

        protected override Dictionary<string, string> DefineCIEnvironmentValues(Dictionary<string, string> values)
        {
            // Base sets Azure CI values. Take those we can reuse for Git Hub
            buildDir = values[CIEnvironmentValues.Constants.AzureBuildSourcesDirectory];
            repo = values[CIEnvironmentValues.Constants.AzureBuildRepositoryUri];
            branch = values[CIEnvironmentValues.Constants.AzureBuildSourceBranch];

            return values;
        }

        private MockTracerAgent GetAgent(List<MockCIVisibilityTest> receivedTests, Action<MockTracerAgent.EvpProxyPayload, List<MockCIVisibilityTest>> processRequest = null)
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
    }
}
