// <copyright file="CiRunCommandTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tools.Runner.IntegrationTests
{
    [Collection(nameof(ConsoleTestsCollection))]
    [EnvironmentVariablesCleaner(
        Configuration.ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath,
        Configuration.ConfigurationKeys.AgentUri,
        Configuration.ConfigurationKeys.AgentHost,
        Configuration.ConfigurationKeys.AgentPort,
        Configuration.ConfigurationKeys.CIVisibility.GitUploadEnabled,
        Configuration.ConfigurationKeys.CIVisibility.ForceAgentsEvpProxy,
        Configuration.ConfigurationKeys.CIVisibility.IntelligentTestRunnerEnabled,
        Configuration.ConfigurationKeys.CIVisibility.CodeCoverage,
        Configuration.ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath,
        Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath,
        Configuration.ConfigurationKeys.CIVisibility.KnownTestsEnabled,
        Configuration.ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled,
        Configuration.ConfigurationKeys.CIVisibility.FlakyRetryEnabled,
        Configuration.ConfigurationKeys.CIVisibility.DynamicInstrumentationEnabled,
        Configuration.ConfigurationKeys.CIVisibility.ImpactedTestsDetectionEnabled,
        Configuration.ConfigurationKeys.CIVisibility.TestManagementEnabled,
        Configuration.ConfigurationKeys.CIVisibility.TestsSkippingEnabled,
        Configuration.ConfigurationKeys.CIVisibility.TestSessionCommand,
        Configuration.ConfigurationKeys.CIVisibility.TestOptimizationRunId,
        Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder,
        Configuration.ConfigurationKeys.CIVisibility.GitCommitSha,
        Configuration.ConfigurationKeys.CIVisibility.GitRepositoryUrl)]
    public class CiRunCommandTests : BaseRunCommandTests
    {
        public CiRunCommandTests()
            : base("ci run", enableCiVisibilityMode: true)
        {
        }

        [Fact]
        public void CoberturaCodeCoverage()
        {
            var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var filePath = Path.Combine(directory, "coverage.cobertura.xml");
            RunExternalCoverageTest(filePath);
        }

        [Fact]
        public void OpenCoverCodeCoverage()
        {
            var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var filePath = Path.Combine(directory, "coverage.opencover.xml");
            RunExternalCoverageTest(filePath);
        }

        [Fact]
        public void RemoteInternalCoverageCreatesCoveragePathWhenSkippingIsEnabled()
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            string command = null;
            string arguments = null;
            Dictionary<string, string> environmentVariables = null;
            bool callbackInvoked = false;
            bool settingsRequestReceived = false;
            var evpPaths = new List<string>();

            Program.CallbackForTests = (c, a, e) =>
            {
                command = c;
                arguments = a;
                environmentVariables = e;
                callbackInvoked = true;
            };

            using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
            agent.EventPlatformProxyPayloadReceived += (sender, args) =>
            {
                evpPaths.Add(args.Value.PathAndQuery);

                if (args.Value.PathAndQuery.EndsWith("api/v2/libraries/tests/services/setting"))
                {
                    settingsRequestReceived = true;
                    args.Value.Response = new MockTracerResponse(
                        """
                        {
                          "data": {
                            "id": "b5a855bffe6c0b2ae5d150fb6ad674363464c816",
                            "type": "ci_app_tracers_test_service_settings",
                            "attributes": {
                              "code_coverage": true,
                              "early_flake_detection": {
                                "enabled": false,
                                "slow_test_retries": {},
                                "faulty_session_threshold": 0
                              },
                              "flaky_test_retries_enabled": false,
                              "itr_enabled": true,
                              "known_tests_enabled": false,
                              "require_git": false,
                              "test_management": {
                                "enabled": false,
                                "attempt_to_fix_retries": 0
                              },
                              "tests_skipping": true
                            }
                          }
                        }
                        """,
                        200);
                }
            };

            var agentUrl = $"http://localhost:{agent.Port}";
            var commandLine = $"{CommandPrefix} dotnet test tests/Sample.Tests/Sample.Tests.csproj --dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url {agentUrl}";

            using var console = ConsoleHelper.Redirect();

            try
            {
                var exitCode = Program.Main(commandLine.Split(' '));

                using var scope = new AssertionScope();

                scope.AddReportable("output", console.Output);
                scope.AddReportable("evp paths", string.Join(Environment.NewLine, evpPaths));

                exitCode.Should().Be(0);
                callbackInvoked.Should().BeTrue();
                settingsRequestReceived.Should().BeTrue();

                command.Should().Be("dotnet");
                arguments.Should().Contain("test tests/Sample.Tests/Sample.Tests.csproj");
                arguments.Should().Contain("--collect DatadogCoverage");
                environmentVariables.Should().NotBeNull();
                environmentVariables.Should().Contain(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "1");
                environmentVariables.Should().ContainKey(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath);
                Directory.Exists(environmentVariables[Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath]).Should().BeTrue();
            }
            finally
            {
                if (environmentVariables?.TryGetValue(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, out var coveragePath) == true &&
                    Directory.Exists(coveragePath))
                {
                    Directory.Delete(coveragePath, recursive: true);
                }
            }
        }

        private void RunExternalCoverageTest(string filePath)
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            string command = null;
            string arguments = null;
            Dictionary<string, string> environmentVariables = null;
            bool callbackInvoked = false;

            Program.CallbackForTests = (c, a, e) =>
            {
                var session = DotnetCommon.CreateSession();
                command = c;
                arguments = a;
                environmentVariables = e;
                callbackInvoked = true;
                DotnetCommon.FinalizeSession(session, 0, null);
            };

            // CI visibility mode checks if there's a running agent
            using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
            MockCIVisibilityTestModule testSession = null;
            agent.EventPlatformProxyPayloadReceived += (sender, args) =>
            {
                if (args.Value.Headers["Content-Type"] != "application/msgpack")
                {
                    return;
                }

                var payload = JsonConvert.DeserializeObject<MockCIVisibilityProtocol>(args.Value.BodyInJson);
                if (payload.Events?.Length > 0)
                {
                    foreach (var @event in payload.Events)
                    {
                        if (@event.Type == SpanTypes.TestSession)
                        {
                            testSession = JsonConvert.DeserializeObject<MockCIVisibilityTestModule>(@event.Content.ToString());
                            break;
                        }
                    }
                }
            };

            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, filePath);

            var agentUrl = $"http://localhost:{agent.Port}";
            var commandLine = $"{CommandPrefix} test.exe --dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url {agentUrl} --set-env VAR1=A --set-env VAR2=B";

            using var console = ConsoleHelper.Redirect();

            var exitCode = Program.Main(commandLine.Split(' '));

            using var scope = new AssertionScope();

            scope.AddReportable("output", console.Output);

            exitCode.Should().Be(0);
            callbackInvoked.Should().BeTrue();

            command.Should().Be("test.exe");
            arguments.Should().BeNullOrEmpty();
            environmentVariables.Should().NotBeNull();

            testSession.Should().NotBeNull();
            testSession.Meta.Should().NotContain(new KeyValuePair<string, string>(CodeCoverageTags.Enabled, "true"));
            testSession.Metrics.Should().Contain(new KeyValuePair<string, double>(CodeCoverageTags.PercentageOfTotalLines, 83.33));
        }

        /// <summary>
        /// Prepares stable settings inputs before Test Optimization builds its cached CI environment for runner tests.
        /// </summary>
        private void PrepareRunnerSettingsInputs()
        {
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.IntelligentTestRunnerEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, null);
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestsSkippingEnabled, null);
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.KnownTestsEnabled, null);
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled, null);
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.FlakyRetryEnabled, null);
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.DynamicInstrumentationEnabled, null);
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ImpactedTestsDetectionEnabled, null);
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestManagementEnabled, null);
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestOptimizationRunId, null);
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, null);
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.GitCommitSha, "0123456789abcdef0123456789abcdef01234567");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.GitRepositoryUrl, "https://github.com/DataDog/dd-trace-dotnet");
            SetCachedCiEnvironmentValue(nameof(CIEnvironmentValues.Commit), "0123456789abcdef0123456789abcdef01234567");
            SetCachedCiEnvironmentValue(nameof(CIEnvironmentValues.Repository), "https://github.com/DataDog/dd-trace-dotnet");
            SetCachedCiEnvironmentValue(nameof(CIEnvironmentValues.Branch), "main");
            SetCachedCiEnvironmentValue(nameof(CIEnvironmentValues.WorkspacePath), Environment.CurrentDirectory);
        }

        /// <summary>
        /// Updates the cached CI environment used by Test Optimization when another inherited runner test initialized it first.
        /// </summary>
        private void SetCachedCiEnvironmentValue(string propertyName, string value)
        {
            typeof(CIEnvironmentValues).GetProperty(propertyName)?.SetValue(CIEnvironmentValues.Instance, value);
        }
    }
}
