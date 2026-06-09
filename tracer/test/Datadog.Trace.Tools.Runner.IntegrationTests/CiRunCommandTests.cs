// <copyright file="CiRunCommandTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tools.Runner.IntegrationTests
{
    [Collection(nameof(ConsoleTestsCollection))]
    [EnvironmentVariablesCleaner(
        Configuration.ConfigurationKeys.DebugEnabled,
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
        Configuration.ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory,
        Configuration.ConfigurationKeys.CIVisibility.TestOptimizationRunId,
        Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip,
        Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand,
        Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillPath,
        Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder,
        Configuration.ConfigurationKeys.CIVisibility.GitCommitSha,
        Configuration.ConfigurationKeys.CIVisibility.GitRepositoryUrl,
        "X_DATADOG_TRACE_ID",
        "X_DATADOG_PARENT_ID",
        "X_DATADOG_SAMPLING_PRIORITY",
        "X_DATADOG_ORIGIN",
        "X_DATADOG_TAGS",
        "TRACEPARENT",
        "TRACESTATE",
        "BAGGAGE",
        "B3",
        "X_B3_TRACEID",
        "X_B3_SPANID",
        "X_B3_SAMPLED",
        "X_B3_FLAGS")]
    public class CiRunCommandTests : BaseRunCommandTests
    {
        /// <summary>
        /// Repository-relative source path used by the backend coverage payload in the Coverlet fallback test.
        /// </summary>
        private const string XUnitSampleSourcePath = "tracer/test/test-applications/integrations/Samples.XUnitTests/TestSuite.cs";

        /// <summary>
        /// Source line covered by the skipped XUnit sample test in the backend coverage payload.
        /// </summary>
        private const int SimplePassTestCoveredLine = 23;
        private const string RunnerOwnedCodeCoverageMarkerFileName = ".datadog-runner-owned-code-coverage";

        private static readonly string[] RunScopedPropagationEnvironmentVariables =
        [
            "X_DATADOG_TRACE_ID",
            "X_DATADOG_PARENT_ID",
            "X_DATADOG_SAMPLING_PRIORITY",
            "X_DATADOG_ORIGIN",
            "X_DATADOG_TAGS",
            "TRACEPARENT",
            "TRACESTATE",
            "BAGGAGE",
            "B3",
            "X_B3_TRACEID",
            "X_B3_SPANID",
            "X_B3_SAMPLED",
            "X_B3_FLAGS"
        ];

        private readonly string _previousCachedCiCommit;
        private readonly string _previousCachedCiRepository;
        private readonly string _previousCachedCiBranch;
        private readonly string _previousCachedCiWorkspacePath;

        public CiRunCommandTests()
            : base("ci run", enableCiVisibilityMode: true)
        {
            _previousCachedCiCommit = CIEnvironmentValues.Instance.Commit;
            _previousCachedCiRepository = CIEnvironmentValues.Instance.Repository;
            _previousCachedCiBranch = CIEnvironmentValues.Instance.Branch;
            _previousCachedCiWorkspacePath = CIEnvironmentValues.Instance.WorkspacePath;
        }

        public override void Dispose()
        {
            RestoreCachedCiEnvironmentValues();
            base.Dispose();
        }

        [Fact]
        public void RunnerSettingsInputsRestoreCachedCiEnvironmentValues()
        {
            PrepareRunnerSettingsInputs();

            CIEnvironmentValues.Instance.Commit.Should().Be("0123456789abcdef0123456789abcdef01234567");
            CIEnvironmentValues.Instance.Repository.Should().Be("https://github.com/DataDog/dd-trace-dotnet");
            CIEnvironmentValues.Instance.Branch.Should().Be("main");
            CIEnvironmentValues.Instance.WorkspacePath.Should().Be(Environment.CurrentDirectory);

            RestoreCachedCiEnvironmentValues();

            CIEnvironmentValues.Instance.Commit.Should().Be(_previousCachedCiCommit);
            CIEnvironmentValues.Instance.Repository.Should().Be(_previousCachedCiRepository);
            CIEnvironmentValues.Instance.Branch.Should().Be(_previousCachedCiBranch);
            CIEnvironmentValues.Instance.WorkspacePath.Should().Be(_previousCachedCiWorkspacePath);
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
        public void ExternalCoberturaCodeCoverageIsBackfilledWhenActualItrSkipExists()
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            using var coverageDirectory = new TemporaryDirectory("dd-ci-external-coverage-");
            var attachmentDirectory = Path.Combine(coverageDirectory.RootPath, Guid.NewGuid().ToString("N"));
            var filePath = Path.Combine(attachmentDirectory, "coverage.cobertura.xml");
            var backfillRunFolder = Path.Combine(coverageDirectory.RootPath, ".dd-backfill");
            using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.AgentUri, $"http://localhost:{agent.Port}");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, filePath);
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestsSkippingEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, "test.exe");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, backfillRunFolder);
            CoverageBackfillCapability.ResetCommandLineCacheForTests();
            TestOptimization.Instance.InitializeFromRunner(TestOptimization.Instance.Settings, NullDiscoveryService.Instance, eventPlatformProxyEnabled: true);
            var previousCurrentSession = TestSession.Current;
            TestSession.Current = null;
            TestSession session = null;

            try
            {
                session = TestSession.GetOrCreate("test.exe", workingDirectory: Environment.CurrentDirectory, framework: null, startDate: null);
                WriteCoverletCollectorCoverageFile(coverageDirectory.RootPath, [SimplePassTestCoveredLine], coverageFile: filePath);
                CoverageBackfillDataStore.Persist(TestOptimization.Instance, CreateCoverageBackfillData(XUnitSampleSourcePath, SimplePassTestCoveredLine));
                CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId);

                DotnetCommon.FinalizeSession(session, 0, null);

                session.HasCodeCoverageResult(CodeCoverageReportSource.ExternalXml).Should().BeTrue();
                session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(100);
                session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().Be("true");
                File.ReadAllText(filePath).Should().Contain($"""<line number="{SimplePassTestCoveredLine}" hits="1" />""");
            }
            finally
            {
                session?.Close(TestStatus.Pass);
                TestSession.Current = previousCurrentSession;
                CoverageBackfillCapability.ResetCommandLineCacheForTests();
                TestOptimization.Instance.Reset();
            }
        }

        [Theory]
        [InlineData("dotnet", "dotnet", "test tests/Sample.Tests/Sample.Tests.csproj")]
        [InlineData("/usr/bin/dotnet", "/usr/bin/dotnet", "test tests/Sample.Tests/Sample.Tests.csproj")]
        [InlineData("dotnet -d", "dotnet", "-d test tests/Sample.Tests/Sample.Tests.csproj")]
        [InlineData("dotnet --diagnostics", "dotnet", "--diagnostics test tests/Sample.Tests/Sample.Tests.csproj")]
        [InlineData("dotnet --info", "dotnet", "--info test tests/Sample.Tests/Sample.Tests.csproj")]
        [InlineData("dotnet --version", "dotnet", "--version test tests/Sample.Tests/Sample.Tests.csproj")]
        public void RemoteInternalCoverageCreatesCoveragePathWhenSkippingIsEnabled(string program, string expectedCommand, string expectedArguments)
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
            var commandLine = $"{CommandPrefix} {program} test tests/Sample.Tests/Sample.Tests.csproj --dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url {agentUrl}";

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

                command.Should().Be(expectedCommand);
                arguments.Should().Contain(expectedArguments);
                arguments.Should().Contain("--collect DatadogCoverage");
                environmentVariables.Should().NotBeNull();
                environmentVariables.Should().Contain(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "1");
                environmentVariables.Should().ContainKey(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath);
                environmentVariables.Should().ContainKey(Configuration.ConfigurationKeys.CIVisibility.TestOptimizationRunId);
                var coveragePath = environmentVariables[Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath];
                Directory.Exists(coveragePath).Should().BeTrue();
                Path.GetFileName(coveragePath).Should().StartWith("datadog-coverage-");
                Path.GetFileName(coveragePath).Should().Contain(environmentVariables[Configuration.ConfigurationKeys.CIVisibility.TestOptimizationRunId]);
                File.Exists(Path.Combine(coveragePath, RunnerOwnedCodeCoverageMarkerFileName)).Should().BeTrue();
            }
            finally
            {
                Program.CallbackForTests = null;
                if (environmentVariables?.TryGetValue(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, out var coveragePath) == true &&
                    Directory.Exists(coveragePath))
                {
                    Directory.Delete(coveragePath, recursive: true);
                }

                TestOptimization.Instance.Reset();
            }
        }

        [Fact]
        public void RemoteInternalCoverageInjectsCollectorWhenDotnetTestComesFromResponseFile()
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            string command = null;
            string arguments = null;
            Dictionary<string, string> environmentVariables = null;
            bool settingsRequestReceived = false;

            Program.CallbackForTests = (c, a, e) =>
            {
                command = c;
                arguments = a;
                environmentVariables = e;
            };

            using var responseDirectory = new TemporaryDirectory("dd-ci-response-file-");
            var responseFilePath = Path.Combine(responseDirectory.RootPath, "test.rsp");
            var responseFileContents =
                """
                # dotnet response files may place the SDK command on a separate line.
                test
                tests/Sample.Tests/Sample.Tests.csproj
                """;
            File.WriteAllText(responseFilePath, responseFileContents);

            using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
            agent.EventPlatformProxyPayloadReceived += (sender, args) =>
            {
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
            var commandLine = new[]
            {
                "ci",
                "run",
                "--dd-env",
                "TestEnv",
                "--dd-service",
                "TestService",
                "--dd-version",
                "TestVersion",
                "--tracer-home",
                "TestTracerHome",
                "--agent-url",
                agentUrl,
                "--",
                "dotnet",
                "@" + responseFilePath
            };

            using var console = ConsoleHelper.Redirect();

            try
            {
                var exitCode = Program.Main(commandLine);

                using var scope = new AssertionScope();
                scope.AddReportable("output", console.Output);

                exitCode.Should().Be(0);
                settingsRequestReceived.Should().BeTrue();
                command.Should().Be("dotnet");
                arguments.Should().Contain("@" + responseFilePath);
                arguments.Should().Contain("--test-adapter-path");
                arguments.Should().Contain("--collect DatadogCoverage");
                environmentVariables.Should().NotBeNull();
                environmentVariables.Should().Contain(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "1");
                environmentVariables.Should().ContainKey(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath);
                var coveragePath = environmentVariables[Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath];
                Directory.Exists(coveragePath).Should().BeTrue();
                Path.GetFileName(coveragePath).Should().StartWith("datadog-coverage-");
                File.Exists(Path.Combine(coveragePath, RunnerOwnedCodeCoverageMarkerFileName)).Should().BeTrue();
            }
            finally
            {
                Program.CallbackForTests = null;
                if (environmentVariables?.TryGetValue(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, out var coveragePath) == true &&
                    Directory.Exists(coveragePath))
                {
                    Directory.Delete(coveragePath, recursive: true);
                }

                TestOptimization.Instance.Reset();
            }
        }

        [Fact]
        public void RemoteInternalCoverageInjectsCollectorWhenDotnetTestComesFromSingleLineResponseFile()
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            string command = null;
            string arguments = null;
            Dictionary<string, string> environmentVariables = null;
            bool settingsRequestReceived = false;

            Program.CallbackForTests = (c, a, e) =>
            {
                command = c;
                arguments = a;
                environmentVariables = e;
            };

            using var responseDirectory = new TemporaryDirectory("dd-ci-response-file-single-line-");
            var responseFilePath = Path.Combine(responseDirectory.RootPath, "test.rsp");
            File.WriteAllText(responseFilePath, "test tests/Sample.Tests/Sample.Tests.csproj");

            using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
            agent.EventPlatformProxyPayloadReceived += (sender, args) =>
            {
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
            var commandLine = new[]
            {
                "ci",
                "run",
                "--dd-env",
                "TestEnv",
                "--dd-service",
                "TestService",
                "--dd-version",
                "TestVersion",
                "--tracer-home",
                "TestTracerHome",
                "--agent-url",
                agentUrl,
                "--",
                "dotnet",
                "@" + responseFilePath
            };

            using var console = ConsoleHelper.Redirect();

            try
            {
                var exitCode = Program.Main(commandLine);

                using var scope = new AssertionScope();
                scope.AddReportable("output", console.Output);

                exitCode.Should().Be(0);
                settingsRequestReceived.Should().BeTrue();
                command.Should().Be("dotnet");
                arguments.Should().Contain("@" + responseFilePath);
                arguments.Should().Contain("--test-adapter-path");
                arguments.Should().Contain("--collect DatadogCoverage");
                environmentVariables.Should().NotBeNull();
                environmentVariables.Should().Contain(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "1");
                environmentVariables.Should().ContainKey(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath);
                var coveragePath = environmentVariables[Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath];
                Directory.Exists(coveragePath).Should().BeTrue();
                File.Exists(Path.Combine(coveragePath, RunnerOwnedCodeCoverageMarkerFileName)).Should().BeTrue();
            }
            finally
            {
                Program.CallbackForTests = null;
                if (environmentVariables?.TryGetValue(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, out var coveragePath) == true &&
                    Directory.Exists(coveragePath))
                {
                    Directory.Delete(coveragePath, recursive: true);
                }

                TestOptimization.Instance.Reset();
            }
        }

        [Fact]
        public void RemoteInternalCoverageInjectsCollectorBeforeDoubleDashFromDotnetTestResponseFile()
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            string command = null;
            string arguments = null;
            string generatedResponseFileContents = null;
            Dictionary<string, string> environmentVariables = null;
            bool settingsRequestReceived = false;

            Program.CallbackForTests = (c, a, e) =>
            {
                command = c;
                arguments = a;
                environmentVariables = e;
                if (a.Trim().Trim('"').StartsWith("@", StringComparison.Ordinal))
                {
                    generatedResponseFileContents = ReadSingleResponseFileArgument(a);
                }
            };

            using var responseDirectory = new TemporaryDirectory("dd-ci-response-file-double-dash-");
            var responseFilePath = Path.Combine(responseDirectory.RootPath, "test.rsp");
            var longRunSettings = "RunConfiguration.TargetFrameworkVersion=net10.0;" + new string('x', 4096);
            var responseFileContents = string.Join(
                Environment.NewLine,
                "test",
                "tests/Sample.Tests/Sample.Tests.csproj",
                "--",
                longRunSettings);
            File.WriteAllText(responseFilePath, responseFileContents);

            using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
            agent.EventPlatformProxyPayloadReceived += (sender, args) =>
            {
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
            var commandLine = new[]
            {
                "ci",
                "run",
                "--dd-env",
                "TestEnv",
                "--dd-service",
                "TestService",
                "--dd-version",
                "TestVersion",
                "--tracer-home",
                "TestTracerHome",
                "--agent-url",
                agentUrl,
                "--",
                "dotnet",
                "@" + responseFilePath
            };

            using var console = ConsoleHelper.Redirect();

            try
            {
                var exitCode = Program.Main(commandLine);

                using var scope = new AssertionScope();
                scope.AddReportable("output", console.Output);

                exitCode.Should().Be(0);
                settingsRequestReceived.Should().BeTrue();
                command.Should().Be("dotnet");
                arguments.Should().StartWith("@");
                arguments.Should().NotContain("@" + responseFilePath);
                arguments.Should().NotContain(longRunSettings);
                generatedResponseFileContents.Should().NotBeNull();
                generatedResponseFileContents.Should().Contain("--test-adapter-path");
                generatedResponseFileContents.Should().Contain("--collect");
                generatedResponseFileContents.Should().Contain("DatadogCoverage");
                generatedResponseFileContents.Should().Contain(longRunSettings);

                var runSettingsSeparatorIndex = generatedResponseFileContents.IndexOf($"{Environment.NewLine}--{Environment.NewLine}", StringComparison.Ordinal);
                runSettingsSeparatorIndex.Should().BeGreaterThan(0);
                generatedResponseFileContents.IndexOf("--test-adapter-path", StringComparison.Ordinal).Should().BeLessThan(runSettingsSeparatorIndex);
                generatedResponseFileContents.IndexOf("DatadogCoverage", StringComparison.Ordinal).Should().BeLessThan(runSettingsSeparatorIndex);

                environmentVariables.Should().NotBeNull();
                environmentVariables.Should().Contain(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "1");
                environmentVariables.Should().ContainKey(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath);
                var coveragePath = environmentVariables[Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath];
                Directory.Exists(coveragePath).Should().BeTrue();
                Path.GetFileName(coveragePath).Should().StartWith("datadog-coverage-");
                File.Exists(Path.Combine(coveragePath, RunnerOwnedCodeCoverageMarkerFileName)).Should().BeTrue();
            }
            finally
            {
                Program.CallbackForTests = null;
                if (environmentVariables?.TryGetValue(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, out var coveragePath) == true &&
                    Directory.Exists(coveragePath))
                {
                    Directory.Delete(coveragePath, recursive: true);
                }

                TestOptimization.Instance.Reset();
            }
        }

        [Theory]
        [InlineData("#Smoke")]
        [InlineData("@Smoke")]
        public void RemoteInternalCoverageQuotesSpecialResponseFileArgumentsWhenRematerializingDotnetTestResponseFile(string specialArgument)
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            string command = null;
            string arguments = null;
            string generatedResponseFileContents = null;
            Dictionary<string, string> environmentVariables = null;
            bool settingsRequestReceived = false;

            Program.CallbackForTests = (c, a, e) =>
            {
                command = c;
                arguments = a;
                environmentVariables = e;
                if (a.Trim().Trim('"').StartsWith("@", StringComparison.Ordinal))
                {
                    generatedResponseFileContents = ReadSingleResponseFileArgument(a);
                }
            };

            using var responseDirectory = new TemporaryDirectory("dd-ci-response-file-special-args-");
            var responseFilePath = Path.Combine(responseDirectory.RootPath, "test.rsp");
            var longRunSettings = "RunConfiguration.TargetFrameworkVersion=net10.0;" + new string('x', 4096);
            var responseFileContents = string.Join(
                Environment.NewLine,
                "test",
                "tests/Sample.Tests/Sample.Tests.csproj",
                "--filter",
                $"\"{specialArgument}\"",
                "--",
                longRunSettings);
            File.WriteAllText(responseFilePath, responseFileContents);

            using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
            agent.EventPlatformProxyPayloadReceived += (sender, args) =>
            {
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
            var commandLine = new[]
            {
                "ci",
                "run",
                "--dd-env",
                "TestEnv",
                "--dd-service",
                "TestService",
                "--dd-version",
                "TestVersion",
                "--tracer-home",
                "TestTracerHome",
                "--agent-url",
                agentUrl,
                "--",
                "dotnet",
                "@" + responseFilePath
            };

            using var console = ConsoleHelper.Redirect();

            try
            {
                var exitCode = Program.Main(commandLine);

                using var scope = new AssertionScope();
                scope.AddReportable("output", console.Output);

                exitCode.Should().Be(0);
                settingsRequestReceived.Should().BeTrue();
                command.Should().Be("dotnet");
                arguments.Should().StartWith("@");
                generatedResponseFileContents.Should().NotBeNull();
                generatedResponseFileContents.Should().Contain($"--filter{Environment.NewLine}\"{specialArgument}\"");
                generatedResponseFileContents.Should().NotContain($"--filter{Environment.NewLine}{specialArgument}{Environment.NewLine}");
                generatedResponseFileContents.Should().Contain(longRunSettings);

                environmentVariables.Should().NotBeNull();
                environmentVariables.Should().Contain(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "1");
                environmentVariables.Should().ContainKey(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath);
                var coveragePath = environmentVariables[Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath];
                Directory.Exists(coveragePath).Should().BeTrue();
                File.Exists(Path.Combine(coveragePath, RunnerOwnedCodeCoverageMarkerFileName)).Should().BeTrue();
            }
            finally
            {
                Program.CallbackForTests = null;
                if (environmentVariables?.TryGetValue(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, out var coveragePath) == true &&
                    Directory.Exists(coveragePath))
                {
                    Directory.Delete(coveragePath, recursive: true);
                }

                TestOptimization.Instance.Reset();
            }
        }

        [Theory]
        [InlineData(new[] { "dotnet", "test", "tests/Sample.Tests/Sample.Tests.csproj", "--coverage" }, "--coverage")]
        [InlineData(new[] { "dotnet", "test", "tests/Sample.Tests/Sample.Tests.csproj", "--coverlet" }, "--coverlet")]
        [InlineData(new[] { "dotnet", "test", "tests/Sample.Tests/Sample.Tests.csproj", "--", "--coverage" }, "--coverage")]
        [InlineData(new[] { "dotnet", "test", "tests/Sample.Tests/Sample.Tests.csproj", "--", "--coverlet" }, "--coverlet")]
        [InlineData(new[] { "dotnet", "test", "tests/Sample.Tests/Sample.Tests.csproj", "-p:TestingPlatformCommandLineArguments=--coverage" }, "TestingPlatformCommandLineArguments=--coverage")]
        [InlineData(new[] { "dotnet", "test", "tests/Sample.Tests/Sample.Tests.csproj", "-p:TestingPlatformCommandLineArguments=--coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura" }, "--coverage-output")]
        public void RemoteInternalCoverageDoesNotInjectVstestCollectorForTestingPlatformCoverage(string[] childCommand, string expectedBackfillCommandFragment)
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestsSkippingEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.KnownTestsEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.DynamicInstrumentationEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ImpactedTestsDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestManagementEnabled, "0");
            string command = null;
            string arguments = null;
            Dictionary<string, string> environmentVariables = null;

            Program.CallbackForTests = (c, a, e) =>
            {
                command = c;
                arguments = a;
                environmentVariables = e;
            };

            using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
            var agentUrl = $"http://localhost:{agent.Port}";
            var commandLine = new List<string>
            {
                "ci",
                "run",
                "--dd-env",
                "TestEnv",
                "--dd-service",
                "TestService",
                "--dd-version",
                "TestVersion",
                "--tracer-home",
                "TestTracerHome",
                "--agent-url",
                agentUrl,
                "--"
            };
            commandLine.AddRange(childCommand);

            using var console = ConsoleHelper.Redirect();

            try
            {
                var exitCode = Program.Main(commandLine.ToArray());

                using var scope = new AssertionScope();
                scope.AddReportable("output", console.Output);

                exitCode.Should().Be(0);
                command.Should().Be("dotnet");
                arguments.Should().NotContain("--test-adapter-path");
                arguments.Should().NotContain("--collect DatadogCoverage");
                environmentVariables.Should().NotBeNull();
                environmentVariables.Should().Contain(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "1");
                environmentVariables.Should().ContainKey(Configuration.ConfigurationKeys.CIVisibility.TestOptimizationRunId);
                environmentVariables.Should().NotContainKey(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath);
                environmentVariables.Should().ContainKey(Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand);
                environmentVariables[Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand].Should().Contain(expectedBackfillCommandFragment);
                environmentVariables[Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand].Should().NotContain("DatadogCoverage");
            }
            finally
            {
                Program.CallbackForTests = null;
                if (environmentVariables?.TryGetValue(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, out var coveragePath) == true &&
                    Directory.Exists(coveragePath))
                {
                    Directory.Delete(coveragePath, recursive: true);
                }

                TestOptimization.Instance.Reset();
            }
        }

        [Fact]
        public void RemoteInternalCoverageInjectsCollectorForBareVstestCommand()
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            string command = null;
            string arguments = null;
            Dictionary<string, string> environmentVariables = null;
            var settingsRequestReceived = false;

            Program.CallbackForTests = (c, a, e) =>
            {
                command = c;
                arguments = a;
                environmentVariables = e;
            };

            using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
            agent.EventPlatformProxyPayloadReceived += (sender, args) =>
            {
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
            var commandLine = $"{CommandPrefix} vstest tests/Sample.Tests/Sample.Tests.dll --dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url {agentUrl}";

            using var console = ConsoleHelper.Redirect();

            try
            {
                var exitCode = Program.Main(commandLine.Split(' '));

                using var scope = new AssertionScope();

                scope.AddReportable("output", console.Output);

                exitCode.Should().Be(0);
                settingsRequestReceived.Should().BeTrue();

                command.Should().Be("vstest");
                arguments.Should().Contain("tests/Sample.Tests/Sample.Tests.dll");
                arguments.Should().Contain("/TestAdapterPath:");
                arguments.Should().Contain("/Collect:DatadogCoverage");
                environmentVariables.Should().NotBeNull();
                environmentVariables.Should().Contain(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "1");
                environmentVariables.Should().ContainKey(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath);
                var coveragePath = environmentVariables[Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath];
                Directory.Exists(coveragePath).Should().BeTrue();
                Path.GetFileName(coveragePath).Should().StartWith("datadog-coverage-");
                File.Exists(Path.Combine(coveragePath, RunnerOwnedCodeCoverageMarkerFileName)).Should().BeTrue();
            }
            finally
            {
                Program.CallbackForTests = null;
                if (environmentVariables?.TryGetValue(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, out var coveragePath) == true &&
                    Directory.Exists(coveragePath))
                {
                    Directory.Delete(coveragePath, recursive: true);
                }

                TestOptimization.Instance.Reset();
            }
        }

        [Fact]
        public void RemoteInternalCoverageInjectsCollectorForDotnetVstestConsoleDll()
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            string command = null;
            string arguments = null;
            Dictionary<string, string> environmentVariables = null;
            var vstestConsolePath = Path.Combine(Path.GetTempPath(), "sdk", "vstest.console.dll");
            var settingsRequestReceived = false;

            Program.CallbackForTests = (c, a, e) =>
            {
                command = c;
                arguments = a;
                environmentVariables = e;
            };

            using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
            agent.EventPlatformProxyPayloadReceived += (sender, args) =>
            {
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
            using var console = ConsoleHelper.Redirect();

            try
            {
                var exitCode = Program.Main(
                    [
                        "ci",
                        "run",
                        "--dd-env",
                        "TestEnv",
                        "--dd-service",
                        "TestService",
                        "--dd-version",
                        "TestVersion",
                        "--tracer-home",
                        "TestTracerHome",
                        "--agent-url",
                        agentUrl,
                        "--",
                        "dotnet",
                        vstestConsolePath,
                        "tests/Sample.Tests/Sample.Tests.dll"
                    ]);

                using var scope = new AssertionScope();
                scope.AddReportable("output", console.Output);

                exitCode.Should().Be(0);
                settingsRequestReceived.Should().BeTrue();
                command.Should().Be("dotnet");
                arguments.Should().Contain(vstestConsolePath);
                arguments.Should().Contain("tests/Sample.Tests/Sample.Tests.dll");
                arguments.Should().Contain("/TestAdapterPath:");
                arguments.Should().Contain("/Collect:DatadogCoverage");
                environmentVariables.Should().NotBeNull();
                environmentVariables.Should().Contain(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "1");
                environmentVariables.Should().ContainKey(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath);
                environmentVariables.Should().ContainKey(Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand);
                environmentVariables[Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand].Should().Contain(vstestConsolePath);
                environmentVariables[Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand].Should().Contain("/Collect:DatadogCoverage");
                var coveragePath = environmentVariables[Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath];
                Directory.Exists(coveragePath).Should().BeTrue();
                File.Exists(Path.Combine(coveragePath, RunnerOwnedCodeCoverageMarkerFileName)).Should().BeTrue();
            }
            finally
            {
                Program.CallbackForTests = null;
                if (environmentVariables?.TryGetValue(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, out var coveragePath) == true &&
                    Directory.Exists(coveragePath))
                {
                    Directory.Delete(coveragePath, recursive: true);
                }

                TestOptimization.Instance.Reset();
            }
        }

        [Fact]
        public void RemoteInternalCoverageInjectsCollectorBeforeDoubleDashFromBareVstestResponseFile()
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            string command = null;
            string arguments = null;
            string generatedResponseFileContents = null;
            Dictionary<string, string> environmentVariables = null;
            var settingsRequestReceived = false;

            Program.CallbackForTests = (c, a, e) =>
            {
                command = c;
                arguments = a;
                environmentVariables = e;
                if (a.Trim().Trim('"').StartsWith("@", StringComparison.Ordinal))
                {
                    generatedResponseFileContents = ReadSingleResponseFileArgument(a);
                }
            };

            using var responseDirectory = new TemporaryDirectory("dd-ci-vstest-response-file-double-dash-");
            var responseFilePath = Path.Combine(responseDirectory.RootPath, "vstest.rsp");
            var longRunSettings = "RunConfiguration.ResultsDirectory=TestResults;" + new string('x', 4096);
            var responseFileContents = string.Join(
                Environment.NewLine,
                "tests/Sample.Tests/Sample.Tests.dll",
                "--",
                longRunSettings);
            File.WriteAllText(responseFilePath, responseFileContents);

            using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
            agent.EventPlatformProxyPayloadReceived += (sender, args) =>
            {
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
            var commandLine = new[]
            {
                "ci",
                "run",
                "--dd-env",
                "TestEnv",
                "--dd-service",
                "TestService",
                "--dd-version",
                "TestVersion",
                "--tracer-home",
                "TestTracerHome",
                "--agent-url",
                agentUrl,
                "--",
                "vstest",
                "@" + responseFilePath
            };

            using var console = ConsoleHelper.Redirect();

            try
            {
                var exitCode = Program.Main(commandLine);

                using var scope = new AssertionScope();

                scope.AddReportable("output", console.Output);

                exitCode.Should().Be(0);
                settingsRequestReceived.Should().BeTrue();

                command.Should().Be("vstest");
                arguments.Should().StartWith("@");
                arguments.Should().NotContain("@" + responseFilePath);
                arguments.Should().NotContain(longRunSettings);
                generatedResponseFileContents.Should().NotBeNull();
                generatedResponseFileContents.Should().Contain("tests/Sample.Tests/Sample.Tests.dll");
                generatedResponseFileContents.Should().Contain("/TestAdapterPath:");
                generatedResponseFileContents.Should().Contain("/Collect:DatadogCoverage");
                generatedResponseFileContents.Should().Contain(longRunSettings);

                var runSettingsSeparatorIndex = generatedResponseFileContents.IndexOf($"{Environment.NewLine}--{Environment.NewLine}", StringComparison.Ordinal);
                runSettingsSeparatorIndex.Should().BeGreaterThan(0);
                generatedResponseFileContents.IndexOf("/TestAdapterPath:", StringComparison.Ordinal).Should().BeLessThan(runSettingsSeparatorIndex);
                generatedResponseFileContents.IndexOf("/Collect:DatadogCoverage", StringComparison.Ordinal).Should().BeLessThan(runSettingsSeparatorIndex);

                environmentVariables.Should().NotBeNull();
                environmentVariables.Should().Contain(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "1");
                environmentVariables.Should().ContainKey(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath);
                var coveragePath = environmentVariables[Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath];
                Directory.Exists(coveragePath).Should().BeTrue();
                Path.GetFileName(coveragePath).Should().StartWith("datadog-coverage-");
                File.Exists(Path.Combine(coveragePath, RunnerOwnedCodeCoverageMarkerFileName)).Should().BeTrue();
            }
            finally
            {
                Program.CallbackForTests = null;
                if (environmentVariables?.TryGetValue(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, out var coveragePath) == true &&
                    Directory.Exists(coveragePath))
                {
                    Directory.Delete(coveragePath, recursive: true);
                }

                TestOptimization.Instance.Reset();
            }
        }

        [Fact]
        public void RemoteInternalCoveragePreservesBareVstestResponseFileWithoutDoubleDash()
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            string command = null;
            string arguments = null;
            Dictionary<string, string> environmentVariables = null;
            var settingsRequestReceived = false;

            Program.CallbackForTests = (c, a, e) =>
            {
                command = c;
                arguments = a;
                environmentVariables = e;
            };

            using var responseDirectory = new TemporaryDirectory("dd-ci-vstest-response-file-");
            var responseFilePath = Path.Combine(responseDirectory.RootPath, "vstest.rsp");
            File.WriteAllText(responseFilePath, "tests/Sample.Tests/Sample.Tests.dll");

            using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
            agent.EventPlatformProxyPayloadReceived += (sender, args) =>
            {
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
            var commandLine = new[]
            {
                "ci",
                "run",
                "--dd-env",
                "TestEnv",
                "--dd-service",
                "TestService",
                "--dd-version",
                "TestVersion",
                "--tracer-home",
                "TestTracerHome",
                "--agent-url",
                agentUrl,
                "--",
                "vstest",
                "@" + responseFilePath
            };

            using var console = ConsoleHelper.Redirect();

            try
            {
                var exitCode = Program.Main(commandLine);

                using var scope = new AssertionScope();

                scope.AddReportable("output", console.Output);

                exitCode.Should().Be(0);
                settingsRequestReceived.Should().BeTrue();

                command.Should().Be("vstest");
                arguments.Should().Contain("@" + responseFilePath);
                arguments.Should().Contain("/TestAdapterPath:");
                arguments.Should().Contain("/Collect:DatadogCoverage");

                environmentVariables.Should().NotBeNull();
                environmentVariables.Should().Contain(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "1");
                environmentVariables.Should().ContainKey(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath);
                var coveragePath = environmentVariables[Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath];
                Directory.Exists(coveragePath).Should().BeTrue();
                Path.GetFileName(coveragePath).Should().StartWith("datadog-coverage-");
                File.Exists(Path.Combine(coveragePath, RunnerOwnedCodeCoverageMarkerFileName)).Should().BeTrue();
            }
            finally
            {
                Program.CallbackForTests = null;
                if (environmentVariables?.TryGetValue(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, out var coveragePath) == true &&
                    Directory.Exists(coveragePath))
                {
                    Directory.Delete(coveragePath, recursive: true);
                }

                TestOptimization.Instance.Reset();
            }
        }

        [Fact]
        public void RemoteInternalCoverageDoesNotDuplicateCollectorForBareVstestCommand()
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            string command = null;
            string arguments = null;
            Dictionary<string, string> environmentVariables = null;
            var settingsRequestReceived = false;

            Program.CallbackForTests = (c, a, e) =>
            {
                command = c;
                arguments = a;
                environmentVariables = e;
            };

            using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
            agent.EventPlatformProxyPayloadReceived += (sender, args) =>
            {
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
            var commandLine = $"{CommandPrefix} vstest tests/Sample.Tests/Sample.Tests.dll /Collect:DatadogCoverage --dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url {agentUrl}";

            using var console = ConsoleHelper.Redirect();

            try
            {
                var exitCode = Program.Main(commandLine.Split(' '));

                using var scope = new AssertionScope();

                scope.AddReportable("output", console.Output);

                exitCode.Should().Be(0);
                settingsRequestReceived.Should().BeTrue();

                command.Should().Be("vstest");
                arguments.Should().Contain("tests/Sample.Tests/Sample.Tests.dll");
                arguments.Should().Contain("/TestAdapterPath:");
                CountArgument(arguments, "/Collect:DatadogCoverage").Should().Be(1);
                environmentVariables.Should().NotBeNull();
                environmentVariables.Should().Contain(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "1");
                environmentVariables.Should().ContainKey(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath);
                var coveragePath = environmentVariables[Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath];
                Directory.Exists(coveragePath).Should().BeTrue();
                Path.GetFileName(coveragePath).Should().StartWith("datadog-coverage-");
                File.Exists(Path.Combine(coveragePath, RunnerOwnedCodeCoverageMarkerFileName)).Should().BeTrue();
            }
            finally
            {
                Program.CallbackForTests = null;
                if (environmentVariables?.TryGetValue(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, out var coveragePath) == true &&
                    Directory.Exists(coveragePath))
                {
                    Directory.Delete(coveragePath, recursive: true);
                }

                TestOptimization.Instance.Reset();
            }
        }

        [Theory]
        [InlineData("test")]
        [InlineData("vstest")]
        public void RemoteInternalCoverageDoesNotTreatDotnetRunApplicationArgumentsAsTestVerb(string applicationArgument)
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestsSkippingEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.KnownTestsEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.DynamicInstrumentationEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ImpactedTestsDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestManagementEnabled, "0");
            string command = null;
            string arguments = null;
            Dictionary<string, string> environmentVariables = null;

            Program.CallbackForTests = (c, a, e) =>
            {
                command = c;
                arguments = a;
                environmentVariables = e;
            };

            using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
            var agentUrl = $"http://localhost:{agent.Port}";

            try
            {
                var exitCode = Program.Main(
                    [
                        "ci",
                        "run",
                        "--dd-env",
                        "TestEnv",
                        "--dd-service",
                        "TestService",
                        "--dd-version",
                        "TestVersion",
                        "--tracer-home",
                        "TestTracerHome",
                        "--agent-url",
                        agentUrl,
                        "--",
                        "dotnet",
                        "run",
                        "--",
                        applicationArgument
                    ]);

                using var scope = new AssertionScope();
                exitCode.Should().Be(0);
                command.Should().Be("dotnet");
                arguments.Should().Be($"run -- {applicationArgument}");
                arguments.Should().NotContain("--test-adapter-path");
                arguments.Should().NotContain("--collect DatadogCoverage");
                arguments.Should().NotContain("/TestAdapterPath:");
                arguments.Should().NotContain("/Collect:DatadogCoverage");
                environmentVariables.Should().NotBeNull();
                environmentVariables.Should().Contain(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "1");
                environmentVariables[Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand].Should().Be($"dotnet run -- {applicationArgument}");
            }
            finally
            {
                Program.CallbackForTests = null;
                if (environmentVariables?.TryGetValue(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, out var coveragePath) == true &&
                    Directory.Exists(coveragePath))
                {
                    Directory.Delete(coveragePath, recursive: true);
                }

                TestOptimization.Instance.Reset();
            }
        }

        [Fact]
        public void RemoteInternalCoverageClearsInheritedRunnerOwnedCodeCoveragePathForItrOnlyRun()
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestsSkippingEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.KnownTestsEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.DynamicInstrumentationEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ImpactedTestsDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestManagementEnabled, "0");
            using var inheritedCoverageParent = new TemporaryDirectory("dd-ci-inherited-coverage-");
            var inheritedCoveragePath = Path.Combine(inheritedCoverageParent.RootPath, "datadog-coverage-2001-02-03_04_05_06-existing-run-id");
            Directory.CreateDirectory(inheritedCoveragePath);
            File.WriteAllText(Path.Combine(inheritedCoveragePath, RunnerOwnedCodeCoverageMarkerFileName), string.Empty);
            EnvironmentHelpers.SetEnvironmentVariable(
                Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath,
                inheritedCoveragePath);
            Dictionary<string, string> environmentVariables = null;

            Program.CallbackForTests = (_, _, e) => environmentVariables = e;

            try
            {
                using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
                var agentUrl = $"http://localhost:{agent.Port}";
                var commandLine = $"{CommandPrefix} dotnet test tests/Sample.Tests/Sample.Tests.csproj --dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url {agentUrl}";

                using var console = ConsoleHelper.Redirect();
                var exitCode = Program.Main(commandLine.Split(' '));

                using var scope = new AssertionScope();
                scope.AddReportable("output", console.Output);

                exitCode.Should().Be(0);
                environmentVariables.Should().NotBeNull();
                environmentVariables.Should().NotContainKey(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath);
                var childEnvironment = Utils.GetProcessStartInfo("dotnet", Environment.CurrentDirectory, environmentVariables).Environment;
                childEnvironment.Should().NotContainKey(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath);
            }
            finally
            {
                Program.CallbackForTests = null;
                TestOptimization.Instance.Reset();
            }
        }

        [Theory]
        [InlineData("VSTest.Console.Arm64")]
        [InlineData("VSTest.Console.Arm64.exe")]
        public void RemoteInternalCoverageAddsDatadogCollectorToArm64VstestConsole(string program)
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

            using var console = ConsoleHelper.Redirect();

            try
            {
                var exitCode = Program.Main(
                    [
                        "ci",
                        "run",
                        "--dd-env",
                        "TestEnv",
                        "--dd-service",
                        "TestService",
                        "--dd-version",
                        "TestVersion",
                        "--tracer-home",
                        "TestTracerHome",
                        "--agent-url",
                        agentUrl,
                        "--",
                        program,
                        "tests/Sample.Tests/Sample.Tests.dll"
                    ]);

                using var scope = new AssertionScope();

                scope.AddReportable("output", console.Output);
                scope.AddReportable("evp paths", string.Join(Environment.NewLine, evpPaths));

                exitCode.Should().Be(0);
                callbackInvoked.Should().BeTrue();
                settingsRequestReceived.Should().BeTrue();

                command.Should().Be(program);
                arguments.Should().Contain("tests/Sample.Tests/Sample.Tests.dll");
                arguments.Should().Contain("/TestAdapterPath:");
                arguments.Should().Contain("/Collect:DatadogCoverage");
                environmentVariables.Should().NotBeNull();
            }
            finally
            {
                Program.CallbackForTests = null;
                if (environmentVariables?.TryGetValue(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, out var coveragePath) == true &&
                    Directory.Exists(coveragePath))
                {
                    Directory.Delete(coveragePath, recursive: true);
                }

                TestOptimization.Instance.Reset();
            }
        }

        [Fact]
        public void RemoteInternalCoveragePreservesUserCodeCoveragePathWithDatadogCoveragePrefix()
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestsSkippingEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.KnownTestsEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.DynamicInstrumentationEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ImpactedTestsDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestManagementEnabled, "0");
            var userCoveragePath = Path.Combine(Path.GetTempPath(), $"datadog-coverage-user-{Guid.NewGuid():N}");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, userCoveragePath);
            Dictionary<string, string> environmentVariables = null;

            Program.CallbackForTests = (_, _, e) => environmentVariables = e;

            try
            {
                using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
                var agentUrl = $"http://localhost:{agent.Port}";
                var commandLine = $"{CommandPrefix} dotnet test tests/Sample.Tests/Sample.Tests.csproj --dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url {agentUrl}";

                using var console = ConsoleHelper.Redirect();
                var exitCode = Program.Main(commandLine.Split(' '));

                using var scope = new AssertionScope();
                scope.AddReportable("output", console.Output);

                exitCode.Should().Be(0);
                environmentVariables.Should().NotBeNull();
                environmentVariables.Should().NotContain(new KeyValuePair<string, string>(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, string.Empty));
                var childEnvironment = Utils.GetProcessStartInfo("dotnet", Environment.CurrentDirectory, environmentVariables).Environment;
                childEnvironment.Should().Contain(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, userCoveragePath);
                EnvironmentHelpers.GetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath).Should().Be(userCoveragePath);
            }
            finally
            {
                Program.CallbackForTests = null;
                TestOptimization.Instance.Reset();
            }
        }

        [Fact]
        public void RemoteInternalCoveragePreservesUserCodeCoveragePathWithRunnerTimestampName()
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestsSkippingEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.KnownTestsEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.DynamicInstrumentationEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ImpactedTestsDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestManagementEnabled, "0");
            using var userCoverageParent = new TemporaryDirectory("dd-ci-user-coverage-");
            var userCoveragePath = Path.Combine(userCoverageParent.RootPath, "datadog-coverage-2001-02-03_04_05_06");
            Directory.CreateDirectory(userCoveragePath);
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, userCoveragePath);
            Dictionary<string, string> environmentVariables = null;

            Program.CallbackForTests = (_, _, e) => environmentVariables = e;

            try
            {
                using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
                var agentUrl = $"http://localhost:{agent.Port}";
                var commandLine = $"{CommandPrefix} dotnet test tests/Sample.Tests/Sample.Tests.csproj --dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url {agentUrl}";

                using var console = ConsoleHelper.Redirect();
                var exitCode = Program.Main(commandLine.Split(' '));

                using var scope = new AssertionScope();
                scope.AddReportable("output", console.Output);

                exitCode.Should().Be(0);
                environmentVariables.Should().NotBeNull();
                environmentVariables.Should().NotContain(new KeyValuePair<string, string>(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, string.Empty));
                var childEnvironment = Utils.GetProcessStartInfo("dotnet", Environment.CurrentDirectory, environmentVariables).Environment;
                childEnvironment.Should().Contain(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, userCoveragePath);
                EnvironmentHelpers.GetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath).Should().Be(userCoveragePath);
            }
            finally
            {
                Program.CallbackForTests = null;
                TestOptimization.Instance.Reset();
            }
        }

        [Fact]
        public void CiRunClearsInheritedPropagatedTraceContextEnvironmentVariables()
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestsSkippingEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.KnownTestsEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.DynamicInstrumentationEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ImpactedTestsDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestManagementEnabled, "0");
            foreach (var key in RunScopedPropagationEnvironmentVariables)
            {
                EnvironmentHelpers.SetEnvironmentVariable(key, "stale");
            }

            Dictionary<string, string> environmentVariables = null;

            Program.CallbackForTests = (_, _, e) => environmentVariables = e;

            try
            {
                using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
                var agentUrl = $"http://localhost:{agent.Port}";
                var commandLine = $"{CommandPrefix} dotnet test tests/Sample.Tests/Sample.Tests.csproj --dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url {agentUrl}";

                using var console = ConsoleHelper.Redirect();
                var exitCode = Program.Main(commandLine.Split(' '));

                using var scope = new AssertionScope();
                scope.AddReportable("output", console.Output);

                exitCode.Should().Be(0);
                environmentVariables.Should().NotBeNull();
                var childEnvironment = Utils.GetProcessStartInfo("dotnet", Environment.CurrentDirectory, environmentVariables).Environment;
                foreach (var key in RunScopedPropagationEnvironmentVariables)
                {
                    environmentVariables.Should().NotContainKey(key);
                    childEnvironment.Should().NotContainKey(key);
                    EnvironmentHelpers.GetEnvironmentVariable(key).Should().BeNull();
                }
            }
            finally
            {
                Program.CallbackForTests = null;
                TestOptimization.Instance.Reset();
            }
        }

        [Fact]
        public void CiRunDoesNotPropagateRunScopedTraceContextFromSetEnv()
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestsSkippingEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.KnownTestsEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.DynamicInstrumentationEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ImpactedTestsDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestManagementEnabled, "0");

            Dictionary<string, string> environmentVariables = null;
            Program.CallbackForTests = (_, _, e) => environmentVariables = e;

            try
            {
                using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
                var agentUrl = $"http://localhost:{agent.Port}";
                var commandLine = $"{CommandPrefix} dotnet test tests/Sample.Tests/Sample.Tests.csproj --dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url {agentUrl} --set-env TRACEPARENT=00-11111111111111111111111111111111-2222222222222222-01 --set-env X_DATADOG_TRACE_ID=123";

                using var console = ConsoleHelper.Redirect();
                var exitCode = Program.Main(commandLine.Split(' '));

                using var scope = new AssertionScope();
                scope.AddReportable("output", console.Output);

                exitCode.Should().Be(0);
                environmentVariables.Should().NotBeNull();
                var childEnvironment = Utils.GetProcessStartInfo("dotnet", Environment.CurrentDirectory, environmentVariables).Environment;
                environmentVariables.Should().NotContainKey("TRACEPARENT");
                environmentVariables.Should().NotContainKey("X_DATADOG_TRACE_ID");
                childEnvironment.Should().NotContainKey("TRACEPARENT");
                childEnvironment.Should().NotContainKey("X_DATADOG_TRACE_ID");
            }
            finally
            {
                Program.CallbackForTests = null;
                TestOptimization.Instance.Reset();
            }
        }

        [Fact]
        public void CiRunDoesNotReuseRunScopedBackfillStateFromSetEnv()
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestsSkippingEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.KnownTestsEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.DynamicInstrumentationEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ImpactedTestsDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestManagementEnabled, "0");

            const string StaleRunId = "stale-run-id-from-set-env";
            Program.CallbackForTests = null;

            try
            {
                using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
                var agentUrl = $"http://localhost:{agent.Port}";
                var commandLine = $"{CommandPrefix} dotnet --version --dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url {agentUrl} --set-env {Configuration.ConfigurationKeys.CIVisibility.TestOptimizationRunId}={StaleRunId}";

                using var console = ConsoleHelper.Redirect();
                var exitCode = Program.Main(commandLine.Split(' '));

                using var scope = new AssertionScope();
                scope.AddReportable("output", console.Output);

                exitCode.Should().Be(0);
                EnvironmentHelpers.GetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestOptimizationRunId).Should().NotBe(StaleRunId);
            }
            finally
            {
                TestOptimization.Instance.Reset();
            }
        }

        [Theory]
        [InlineData("-p:VSTestCollect=DatadogCoverage")]
        [InlineData("/p:VSTestCollect=DatadogCoverage")]
        [InlineData("--property:VSTestCollect=DatadogCoverage")]
        [InlineData("-property:VSTestCollect=MyCustomCollector;DatadogCoverage")]
        public void CiRunDoesNotAddDatadogCoverageCollectorWhenVSTestCollectAlreadySelectsIt(string collectArgument)
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestsSkippingEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.KnownTestsEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.DynamicInstrumentationEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ImpactedTestsDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestManagementEnabled, "0");
            string arguments = null;
            Dictionary<string, string> environmentVariables = null;

            Program.CallbackForTests = (_, a, e) =>
            {
                arguments = a;
                environmentVariables = e;
            };

            try
            {
                using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
                var agentUrl = $"http://localhost:{agent.Port}";
                var commandLine = $"{CommandPrefix} dotnet test tests/Sample.Tests/Sample.Tests.csproj {collectArgument} --dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url {agentUrl}";

                using var console = ConsoleHelper.Redirect();
                var exitCode = Program.Main(commandLine.Split(' '));

                using var scope = new AssertionScope();
                scope.AddReportable("output", console.Output);

                exitCode.Should().Be(0);
                arguments.Should().Contain(collectArgument);
                arguments.Should().Contain("--test-adapter-path");
                CountArgument(arguments, "--collect").Should().Be(0);
                environmentVariables.Should().NotBeNull();
                environmentVariables.Should().ContainKey(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath);
            }
            finally
            {
                Program.CallbackForTests = null;
                if (environmentVariables?.TryGetValue(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, out var coveragePath) == true &&
                    Directory.Exists(coveragePath))
                {
                    Directory.Delete(coveragePath, recursive: true);
                }

                TestOptimization.Instance.Reset();
            }
        }

        [Fact]
        public void RemoteInternalCoveragePreservesExplicitCodeCoveragePath()
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestsSkippingEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.KnownTestsEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.DynamicInstrumentationEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ImpactedTestsDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestManagementEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(
                Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath,
                Path.Combine(Path.GetTempPath(), $"datadog-coverage-stale-{Guid.NewGuid():N}"));
            Dictionary<string, string> environmentVariables = null;

            Program.CallbackForTests = (_, _, e) => environmentVariables = e;

            using var coverageDirectory = new TemporaryDirectory("dd-ci-explicit-coverage-");
            try
            {
                using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
                var agentUrl = $"http://localhost:{agent.Port}";
                var commandLine = $"{CommandPrefix} dotnet test tests/Sample.Tests/Sample.Tests.csproj --dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url {agentUrl} --set-env {Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath}={coverageDirectory.RootPath}";

                using var console = ConsoleHelper.Redirect();
                var exitCode = Program.Main(commandLine.Split(' '));

                using var scope = new AssertionScope();
                scope.AddReportable("output", console.Output);

                exitCode.Should().Be(0);
                environmentVariables.Should().NotBeNull();
                environmentVariables.Should().Contain(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, coverageDirectory.RootPath);
            }
            finally
            {
                Program.CallbackForTests = null;
                TestOptimization.Instance.Reset();
            }
        }

        /// <summary>
        /// Verifies that the runner propagates a shell-safe backfill command when the child program path contains whitespace.
        /// </summary>
        [Fact]
        public void RemoteInternalCoverageBackfillCommandQuotesProgramPathWithWhitespace()
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
            var program = Path.Combine(Path.GetTempPath(), "datadog dotnet tools", "dotnet.exe");

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

            using var console = ConsoleHelper.Redirect();

            try
            {
                var exitCode = Program.Main(
                    [
                        "ci",
                        "run",
                        "--dd-env",
                        "TestEnv",
                        "--dd-service",
                        "TestService",
                        "--dd-version",
                        "TestVersion",
                        "--tracer-home",
                        "TestTracerHome",
                        "--agent-url",
                        agentUrl,
                        "--",
                        program,
                        "test",
                        "tests/Sample.Tests/Sample.Tests.csproj"
                    ]);

                using var scope = new AssertionScope();

                scope.AddReportable("output", console.Output);
                scope.AddReportable("evp paths", string.Join(Environment.NewLine, evpPaths));

                exitCode.Should().Be(0);
                callbackInvoked.Should().BeTrue();
                settingsRequestReceived.Should().BeTrue();

                command.Should().Be(program);
                arguments.Should().Contain("test tests/Sample.Tests/Sample.Tests.csproj");
                arguments.Should().Contain("--collect DatadogCoverage");
                environmentVariables.Should().NotBeNull();
                environmentVariables.Should().ContainKey(Configuration.ConfigurationKeys.CIVisibility.TestOptimizationRunId);
                environmentVariables[Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand].Should().StartWith($"\"{program}\" test tests/Sample.Tests/Sample.Tests.csproj");
                environmentVariables[Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand].Should().Contain("--collect DatadogCoverage");
            }
            finally
            {
                Program.CallbackForTests = null;
                if (environmentVariables?.TryGetValue(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, out var coveragePath) == true &&
                    Directory.Exists(coveragePath))
                {
                    Directory.Delete(coveragePath, recursive: true);
                }
            }
        }

        /// <summary>
        /// Verifies that the runner finalizer rewrites Coverlet collector XML attachments when the collector cannot report coverage through IPC.
        /// </summary>
        /// <param name="useScopedBackfill">Whether the backend coverage and actual-skip marker should be scoped like a testhost request.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CoverletCollectorXmlCoverageIsBackfilledWhenIpcCoverageIsUnavailable(bool useScopedBackfill)
        {
            var scope = new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a");
            var result = RunCoverletCollectorXmlCoverageScenario(
                () =>
                {
                    var backfillData = CreateCoverageBackfillData(XUnitSampleSourcePath, SimplePassTestCoveredLine);
                    if (useScopedBackfill)
                    {
                        CoverageBackfillDataStore.Persist(TestOptimization.Instance, scope, backfillData);
                        CoverageBackfillDataStore.RecordActualItrSkip(scope);
                        CoverageBackfillDataStore.RecordBackfillableItrSkipScope(scope);
                    }
                    else
                    {
                        CoverageBackfillDataStore.Persist(TestOptimization.Instance, backfillData);
                        CoverageBackfillDataStore.RecordActualItrSkip();
                    }

                    CoverageBackfillDataStore.RecordCoverageIpcFailure(nameof(CodeCoverageReportSource.Coverlet));
                },
                SimplePassTestCoveredLine);

            using var scopeAssertions = new AssertionScope();

            result.InitialXml.Should().Contain($"""<line number="{SimplePassTestCoveredLine}" hits="0" />""");
            result.FinalXml.Should().Contain($"""<line number="{SimplePassTestCoveredLine}" hits="1" />""");
            result.FinalXml.Should().Contain("lines-covered=\"1\"");
            result.FinalXml.Should().Contain("line-rate=\"1\"");
            result.TestSession.Metrics.Should().Contain(new KeyValuePair<string, double>(CodeCoverageTags.PercentageOfTotalLines, 100));
            result.TestSession.Meta.Should().Contain(CodeCoverageTags.Backfilled, "true");
        }

        /// <summary>
        /// Verifies that the runner finalizer also processes Coverlet collector OpenCover attachments when IPC is unavailable.
        /// </summary>
        [Fact]
        public void CoverletCollectorXmlCoverageProcessesOpenCoverWhenIpcCoverageIsUnavailable()
        {
            var result = RunCoverletCollectorXmlCoverageScenario(
                () =>
                {
                    CoverageBackfillDataStore.Persist(TestOptimization.Instance, CreateCoverageBackfillData(XUnitSampleSourcePath, SimplePassTestCoveredLine));
                    CoverageBackfillDataStore.RecordActualItrSkip();
                    CoverageBackfillDataStore.RecordCoverageIpcFailure(nameof(CodeCoverageReportSource.Coverlet));
                },
                useOpenCoverReport: true,
                SimplePassTestCoveredLine);

            using var scopeAssertions = new AssertionScope();

            result.InitialXml.Should().Contain($"""<SequencePoint vc="0" uspid="{SimplePassTestCoveredLine}" ordinal="0" sl="{SimplePassTestCoveredLine}" """);
            result.FinalXml.Should().Contain($"""<SequencePoint vc="1" uspid="{SimplePassTestCoveredLine}" ordinal="0" sl="{SimplePassTestCoveredLine}" """);
            result.FinalXml.Should().Contain("sequenceCoverage=\"100\"");
            result.TestSession.Metrics.Should().Contain(new KeyValuePair<string, double>(CodeCoverageTags.PercentageOfTotalLines, 100));
            result.TestSession.Meta.Should().Contain(CodeCoverageTags.Backfilled, "true");
        }

        /// <summary>
        /// Verifies that the runner only loads scoped backend coverage for scopes that actually skipped tests by ITR.
        /// </summary>
        [Fact]
        public void CoverletCollectorXmlCoverageBackfillsOnlyScopesWithActualItrSkips()
        {
            var skippedScope = new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a");
            var unskippedScope = new SkippableTestsRequestScope("Samples.XUnitTests", "scope-b");
            var otherCoveredLine = 26;

            var result = RunCoverletCollectorXmlCoverageScenario(
                () =>
                {
                    CoverageBackfillDataStore.Persist(TestOptimization.Instance, skippedScope, CreateCoverageBackfillData(XUnitSampleSourcePath, SimplePassTestCoveredLine));
                    CoverageBackfillDataStore.Persist(TestOptimization.Instance, unskippedScope, CreateCoverageBackfillData(XUnitSampleSourcePath, otherCoveredLine));
                    CoverageBackfillDataStore.RecordActualItrSkip(skippedScope);
                    CoverageBackfillDataStore.RecordBackfillableItrSkipScope(skippedScope);
                    CoverageBackfillDataStore.RecordCoverageIpcFailure(nameof(CodeCoverageReportSource.Coverlet));
                },
                SimplePassTestCoveredLine,
                otherCoveredLine);

            using var scopeAssertions = new AssertionScope();

            result.InitialXml.Should().Contain($"""<line number="{SimplePassTestCoveredLine}" hits="0" />""");
            result.InitialXml.Should().Contain($"""<line number="{otherCoveredLine}" hits="0" />""");
            result.FinalXml.Should().Contain($"""<line number="{SimplePassTestCoveredLine}" hits="1" />""");
            result.FinalXml.Should().Contain($"""<line number="{otherCoveredLine}" hits="0" />""");
            result.FinalXml.Should().Contain("lines-covered=\"1\"");
            result.FinalXml.Should().Contain("line-rate=\"0.5\"");
            result.TestSession.Metrics.Should().Contain(new KeyValuePair<string, double>(CodeCoverageTags.PercentageOfTotalLines, 50));
            result.TestSession.Meta.Should().Contain(CodeCoverageTags.Backfilled, "true");
        }

        /// <summary>
        /// Verifies that selected Coverlet collector XML reports are validated as a set when backend coverage is split across reports.
        /// </summary>
        [Fact]
        public void CoverletCollectorXmlCoverageBackfillsBackendLinesSplitAcrossReports()
        {
            var otherCoveredLine = 26;
            var result = RunCoverletCollectorXmlCoverageScenario(
                () =>
                {
                    CoverageBackfillDataStore.Persist(TestOptimization.Instance, CreateCoverageBackfillData(XUnitSampleSourcePath, SimplePassTestCoveredLine, otherCoveredLine));
                    CoverageBackfillDataStore.RecordActualItrSkip();
                    CoverageBackfillDataStore.RecordCoverageIpcFailure(nameof(CodeCoverageReportSource.Coverlet));
                },
                resultsDirectory => WriteCoverletCollectorCoverageFile(resultsDirectory, [otherCoveredLine]),
                SimplePassTestCoveredLine);

            using var scopeAssertions = new AssertionScope();

            var backfilledPrimaryReport = false;
            var backfilledSecondaryReport = false;
            result.InitialXmlByPath.Should().HaveCount(2);
            foreach (var initialXmlByPath in result.InitialXmlByPath)
            {
                result.FinalXmlByPath.Should().ContainKey(initialXmlByPath.Key);
                if (initialXmlByPath.Value.Contains($"""<line number="{SimplePassTestCoveredLine}" hits="0" />"""))
                {
                    result.FinalXmlByPath[initialXmlByPath.Key].Should().Contain($"""<line number="{SimplePassTestCoveredLine}" hits="1" />""");
                    backfilledPrimaryReport = true;
                }

                if (initialXmlByPath.Value.Contains($"""<line number="{otherCoveredLine}" hits="0" />"""))
                {
                    result.FinalXmlByPath[initialXmlByPath.Key].Should().Contain($"""<line number="{otherCoveredLine}" hits="1" />""");
                    backfilledSecondaryReport = true;
                }
            }

            backfilledPrimaryReport.Should().BeTrue();
            backfilledSecondaryReport.Should().BeTrue();
            result.FinalXml.Should().Contain($"""<line number="{SimplePassTestCoveredLine}" hits="1" />""");
            result.TestSession.Metrics.Should().Contain(new KeyValuePair<string, double>(CodeCoverageTags.PercentageOfTotalLines, 100));
            result.TestSession.Meta.Should().Contain(CodeCoverageTags.Backfilled, "true");
        }

        /// <summary>
        /// Verifies that selected Coverlet collector XML fallback allows legitimate overlap on a backend line.
        /// </summary>
        [Fact]
        public void CoverletCollectorXmlCoverageBackfillsSelectedReportsThatOverlapBackendLines()
        {
            var otherUncoveredLine = 26;
            var result = RunCoverletCollectorXmlCoverageScenario(
                () =>
                {
                    CoverageBackfillDataStore.Persist(TestOptimization.Instance, CreateCoverageBackfillData(XUnitSampleSourcePath, SimplePassTestCoveredLine));
                    CoverageBackfillDataStore.RecordActualItrSkip();
                    CoverageBackfillDataStore.RecordCoverageIpcFailure(nameof(CodeCoverageReportSource.Coverlet));
                },
                resultsDirectory => WriteCoverletCollectorCoverageFile(resultsDirectory, [SimplePassTestCoveredLine, otherUncoveredLine]),
                SimplePassTestCoveredLine);

            using var scopeAssertions = new AssertionScope();

            result.InitialXmlByPath.Should().HaveCount(2);
            foreach (var initialXmlByPath in result.InitialXmlByPath)
            {
                result.FinalXmlByPath.Should().ContainKey(initialXmlByPath.Key);
                if (initialXmlByPath.Value.Contains($"""<line number="{SimplePassTestCoveredLine}" hits="0" />"""))
                {
                    result.FinalXmlByPath[initialXmlByPath.Key].Should().Contain($"""<line number="{SimplePassTestCoveredLine}" hits="1" />""");
                }

                if (initialXmlByPath.Value.Contains($"""<line number="{otherUncoveredLine}" hits="0" />"""))
                {
                    result.FinalXmlByPath[initialXmlByPath.Key].Should().Contain($"""<line number="{otherUncoveredLine}" hits="0" />""");
                }
            }

            result.TestSession.Metrics.Should().Contain(new KeyValuePair<string, double>(CodeCoverageTags.PercentageOfTotalLines, 50));
            result.TestSession.Meta.Should().Contain(CodeCoverageTags.Backfilled, "true");
        }

        /// <summary>
        /// Verifies that stale partial Coverlet IPC coverage is not published when selected XML fallback fails closed.
        /// </summary>
        [Fact]
        public void CoverletCollectorXmlCoverageSuppressesPartialCoverletIpcWhenXmlFallbackFailsClosed()
        {
            var result = RunCoverletCollectorXmlCoverageScenario(
                () =>
                {
                    CoverageBackfillDataStore.Persist(TestOptimization.Instance, CreateCoverageBackfillData(XUnitSampleSourcePath, SimplePassTestCoveredLine));
                    CoverageBackfillDataStore.RecordActualItrSkip();
                    CoverageBackfillDataStore.RecordCoverageIpcFailure(nameof(CodeCoverageReportSource.Coverlet));
                },
                session => session.RecordCodeCoverage(
                    CodeCoverageReportSource.Coverlet,
                    percentage: 0,
                    backfilled: false,
                    executableLines: 1,
                    coveredLines: 0),
                CorruptCoverletCollectorCoverageFiles,
                SimplePassTestCoveredLine);

            using var scopeAssertions = new AssertionScope();

            result.InitialXmlByPath.Should().HaveCount(1);
            foreach (var initialXmlByPath in result.InitialXmlByPath)
            {
                result.FinalXmlByPath.Should().ContainKey(initialXmlByPath.Key);
                result.FinalXmlByPath[initialXmlByPath.Key].Should().Be(initialXmlByPath.Value);
            }

            result.TestSession.Metrics.Should().NotContainKey(CodeCoverageTags.PercentageOfTotalLines);
            result.TestSession.Meta.Should().NotContainKey(CodeCoverageTags.Backfilled);
        }

        /// <summary>
        /// Verifies that selected XML fallback failures do not discard Coverlet IPC coverage that already validated backend ITR coverage.
        /// </summary>
        [Fact]
        public void CoverletCollectorXmlCoverageKeepsValidatedCoverletIpcWhenXmlFallbackFailsClosed()
        {
            var result = RunCoverletCollectorXmlCoverageScenario(
                () =>
                {
                    CoverageBackfillDataStore.Persist(TestOptimization.Instance, CreateCoverageBackfillData(XUnitSampleSourcePath, SimplePassTestCoveredLine));
                    CoverageBackfillDataStore.RecordActualItrSkip();
                    CoverageBackfillDataStore.RecordCoverageIpcFailure(nameof(CodeCoverageReportSource.Coverlet));
                },
                session => session.RecordCodeCoverage(
                    CodeCoverageReportSource.Coverlet,
                    percentage: 100,
                    backfilled: true,
                    executableLines: 1,
                    coveredLines: 1,
                    backfillValidated: true),
                CorruptCoverletCollectorCoverageFiles,
                SimplePassTestCoveredLine);

            using var scopeAssertions = new AssertionScope();

            result.InitialXmlByPath.Should().HaveCount(1);
            foreach (var initialXmlByPath in result.InitialXmlByPath)
            {
                result.FinalXmlByPath.Should().ContainKey(initialXmlByPath.Key);
                result.FinalXmlByPath[initialXmlByPath.Key].Should().Be(initialXmlByPath.Value);
            }

            result.TestSession.Metrics.Should().Contain(new KeyValuePair<string, double>(CodeCoverageTags.PercentageOfTotalLines, 100));
            result.TestSession.Meta.Should().Contain(CodeCoverageTags.Backfilled, "true");
        }

        /// <summary>
        /// Verifies that Coverlet collector XML fallback still publishes its result when lower-priority internal coverage was recorded first.
        /// </summary>
        [Fact]
        public void CoverletCollectorXmlCoverageWinsOverExistingInternalCoverage()
        {
            var result = RunCoverletCollectorXmlCoverageScenario(
                () =>
                {
                    CoverageBackfillDataStore.Persist(TestOptimization.Instance, CreateCoverageBackfillData(XUnitSampleSourcePath, SimplePassTestCoveredLine));
                    CoverageBackfillDataStore.RecordActualItrSkip();
                    CoverageBackfillDataStore.RecordCoverageIpcFailure(nameof(CodeCoverageReportSource.Coverlet));
                },
                session => session.RecordCodeCoverage(
                    CodeCoverageReportSource.DatadogInternal,
                    percentage: 0,
                    backfilled: false,
                    executableLines: 1,
                    coveredLines: 0),
                SimplePassTestCoveredLine);

            using var scopeAssertions = new AssertionScope();

            result.FinalXml.Should().Contain($"""<line number="{SimplePassTestCoveredLine}" hits="1" />""");
            result.TestSession.Metrics.Should().Contain(new KeyValuePair<string, double>(CodeCoverageTags.PercentageOfTotalLines, 100));
            result.TestSession.Meta.Should().Contain(CodeCoverageTags.Backfilled, "true");
        }

        /// <summary>
        /// Verifies that Coverlet collector XML fallback still publishes when lower-priority Coverlet IPC delivered only a partial result.
        /// </summary>
        [Fact]
        public void CoverletCollectorXmlCoverageWinsOverPartialCoverletIpcCoverage()
        {
            var result = RunCoverletCollectorXmlCoverageScenario(
                () =>
                {
                    CoverageBackfillDataStore.Persist(TestOptimization.Instance, CreateCoverageBackfillData(XUnitSampleSourcePath, SimplePassTestCoveredLine));
                    CoverageBackfillDataStore.RecordActualItrSkip();
                    CoverageBackfillDataStore.RecordCoverageIpcFailure(nameof(CodeCoverageReportSource.Coverlet));
                },
                session => session.RecordCodeCoverage(
                    CodeCoverageReportSource.Coverlet,
                    percentage: 0,
                    backfilled: false,
                    executableLines: 1,
                    coveredLines: 0),
                SimplePassTestCoveredLine);

            using var scopeAssertions = new AssertionScope();

            result.FinalXml.Should().Contain($"""<line number="{SimplePassTestCoveredLine}" hits="1" />""");
            result.TestSession.Metrics.Should().Contain(new KeyValuePair<string, double>(CodeCoverageTags.PercentageOfTotalLines, 100));
            result.TestSession.Meta.Should().Contain(CodeCoverageTags.Backfilled, "true");
        }

        /// <summary>
        /// Verifies that Datadog internal coverage ignores backend-only files and still publishes local coverage.
        /// </summary>
        [Fact]
        public void DatadogInternalCoverageIgnoresBackendPathThatDoesNotMatch()
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestsSkippingEnabled, "1");

            using var coverageDirectory = new TemporaryDirectory("dd-ci-internal-coverage-");
            using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
            var backfillRunFolder = Path.Combine(coverageDirectory.RootPath, ".dd-backfill");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.AgentUri, $"http://localhost:{agent.Port}");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, coverageDirectory.RootPath);
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, "dotnet test --collect DatadogCoverage");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, backfillRunFolder);
            CoverageBackfillCapability.ResetCommandLineCacheForTests();
            TestOptimization.Instance.InitializeFromRunner(TestOptimization.Instance.Settings, NullDiscoveryService.Instance, eventPlatformProxyEnabled: true);

            File.WriteAllText(
                Path.Combine(coverageDirectory.RootPath, "coverage-input.json"),
                JsonHelper.SerializeObject(CreateGlobalCoverage("src/Calculator.cs")));
            CoverageBackfillDataStore.Persist(TestOptimization.Instance, CreateCoverageBackfillData("src/Other.cs", SimplePassTestCoveredLine));
            CoverageBackfillDataStore.RecordActualItrSkip();

            var previousCurrentSession = TestSession.Current;
            TestSession.Current = null;
            TestSession session = null;
            try
            {
                session = TestSession.GetOrCreate("dotnet test", workingDirectory: Environment.CurrentDirectory, framework: null, startDate: null);
                DotnetCommon.FinalizeSession(session, 0, null);

                session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(0);
                session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
                Directory.GetFiles(coverageDirectory.RootPath, "session-coverage-*.json").Should().ContainSingle();
            }
            finally
            {
                session?.Close(TestStatus.Pass);
                TestSession.Current = previousCurrentSession;
                CoverageBackfillCapability.ResetCommandLineCacheForTests();
                TestOptimization.Instance.Reset();
            }
        }

        /// <summary>
        /// Verifies that backend coverage data is merged when test skipping is enabled, even without an actual-skip marker.
        /// </summary>
        [Fact]
        public void CoverletCollectorXmlCoverageBackfillsWithoutActualItrSkip()
        {
            var result = RunCoverletCollectorXmlCoverageScenario(
                () =>
                {
                    CoverageBackfillDataStore.Persist(TestOptimization.Instance, CreateCoverageBackfillData(XUnitSampleSourcePath, SimplePassTestCoveredLine));
                    CoverageBackfillDataStore.RecordCoverageIpcFailure(nameof(CodeCoverageReportSource.Coverlet));
                },
                SimplePassTestCoveredLine);

            using var scopeAssertions = new AssertionScope();

            result.FinalXml.Should().Contain($"""<line number="{SimplePassTestCoveredLine}" hits="1" />""");
            result.FinalXml.Should().Contain("lines-covered=\"1\"");
            result.TestSession.Metrics.Should().Contain(new KeyValuePair<string, double>(CodeCoverageTags.PercentageOfTotalLines, 100));
            result.TestSession.Meta.Should().Contain(CodeCoverageTags.Backfilled, "true");
        }

        /// <summary>
        /// Verifies that the runner ignores backend-only coverage when it cannot be matched to the Coverlet XML source path.
        /// </summary>
        [Fact]
        public void CoverletCollectorXmlCoverageIgnoresBackendPathThatDoesNotMatch()
        {
            var result = RunCoverletCollectorXmlCoverageScenario(
                () =>
                {
                    var backfillData = CreateCoverageBackfillData("src/Other.cs", SimplePassTestCoveredLine);
                    CoverageBackfillDataStore.Persist(TestOptimization.Instance, backfillData);
                    CoverageBackfillDataStore.RecordActualItrSkip();
                    CoverageBackfillDataStore.RecordCoverageIpcFailure(nameof(CodeCoverageReportSource.Coverlet));
                },
                SimplePassTestCoveredLine);

            using var scopeAssertions = new AssertionScope();

            result.FinalXml.Should().Contain($"""<line number="{SimplePassTestCoveredLine}" hits="0" />""");
            result.FinalXml.Should().Contain("lines-covered=\"0\"");
            result.TestSession.Metrics.Should().Contain(new KeyValuePair<string, double>(CodeCoverageTags.PercentageOfTotalLines, 0));
            result.TestSession.Meta.Should().NotContainKey(CodeCoverageTags.Backfilled);
        }

        /// <summary>
        /// Verifies that an unselected lower-priority report is still rewritten when selected reports can be published.
        /// </summary>
        [Fact]
        public void CoverletCollectorXmlCoverageBackfillsUnselectedReportWhenSelectedReportsCanBeBackfilled()
        {
            string unselectedCoverageFile = null;
            Action<string> writeLowerPriorityReportInSelectedAttachment = resultsDirectory =>
            {
                var selectedCoverageFiles = Directory.GetFiles(resultsDirectory, "coverage.cobertura.xml", SearchOption.AllDirectories);
                selectedCoverageFiles.Should().HaveCount(1);
                var selectedCoverageFile = selectedCoverageFiles[0];
                var selectedAttachmentDirectory = Path.GetDirectoryName(selectedCoverageFile);
                if (selectedAttachmentDirectory is null)
                {
                    throw new InvalidOperationException("Could not resolve selected coverage attachment directory.");
                }

                unselectedCoverageFile = WriteCoverletCollectorOpenCoverCoverageFileInAttachmentDirectory(selectedAttachmentDirectory, [SimplePassTestCoveredLine]);
            };

            var result = RunCoverletCollectorXmlCoverageScenario(
                () =>
                {
                    var backfillData = CreateCoverageBackfillData(XUnitSampleSourcePath, SimplePassTestCoveredLine);
                    CoverageBackfillDataStore.Persist(TestOptimization.Instance, backfillData);
                    CoverageBackfillDataStore.RecordActualItrSkip();
                    CoverageBackfillDataStore.RecordCoverageIpcFailure(nameof(CodeCoverageReportSource.Coverlet));
                },
                writeLowerPriorityReportInSelectedAttachment,
                SimplePassTestCoveredLine);

            using var scopeAssertions = new AssertionScope();

            result.FinalXml.Should().Contain($"""<line number="{SimplePassTestCoveredLine}" hits="1" />""");
            result.InitialXmlByPath[unselectedCoverageFile].Should().Contain($"""<SequencePoint vc="0" uspid="{SimplePassTestCoveredLine}" ordinal="0" sl="{SimplePassTestCoveredLine}" """);
            result.FinalXmlByPath[unselectedCoverageFile].Should().Contain($"""<SequencePoint vc="1" uspid="{SimplePassTestCoveredLine}" ordinal="0" sl="{SimplePassTestCoveredLine}" """);
            result.TestSession.Metrics.Should().Contain(new KeyValuePair<string, double>(CodeCoverageTags.PercentageOfTotalLines, 100));
            result.TestSession.Meta.Should().Contain(CodeCoverageTags.Backfilled, "true");
        }

        /// <summary>
        /// Verifies that lower-priority reports are validated as a set when backend coverage is split across attachment directories.
        /// </summary>
        [Fact]
        public void CoverletCollectorXmlCoverageBackfillsUnselectedReportsSplitAcrossReports()
        {
            var otherCoveredLine = 26;
            var unselectedCoverageFiles = new List<string>();
            Action<string> writeSplitSelectedAndUnselectedReports = resultsDirectory =>
            {
                WriteCoverletCollectorCoverageFile(resultsDirectory, [otherCoveredLine]);
                foreach (var selectedCoverageFile in Directory.GetFiles(resultsDirectory, "coverage.cobertura.xml", SearchOption.AllDirectories))
                {
                    var selectedAttachmentDirectory = Path.GetDirectoryName(selectedCoverageFile);
                    if (selectedAttachmentDirectory is null)
                    {
                        throw new InvalidOperationException("Could not resolve selected coverage attachment directory.");
                    }

                    var selectedXml = File.ReadAllText(selectedCoverageFile);
                    var openCoverLine = selectedXml.Contains($"""<line number="{SimplePassTestCoveredLine}" hits="0" />""") ?
                                            SimplePassTestCoveredLine :
                                            otherCoveredLine;
                    unselectedCoverageFiles.Add(WriteCoverletCollectorOpenCoverCoverageFileInAttachmentDirectory(selectedAttachmentDirectory, [openCoverLine]));
                }
            };

            var result = RunCoverletCollectorXmlCoverageScenario(
                () =>
                {
                    var backfillData = CreateCoverageBackfillData(XUnitSampleSourcePath, SimplePassTestCoveredLine, otherCoveredLine);
                    CoverageBackfillDataStore.Persist(TestOptimization.Instance, backfillData);
                    CoverageBackfillDataStore.RecordActualItrSkip();
                    CoverageBackfillDataStore.RecordCoverageIpcFailure(nameof(CodeCoverageReportSource.Coverlet));
                },
                writeSplitSelectedAndUnselectedReports,
                SimplePassTestCoveredLine);

            using var scopeAssertions = new AssertionScope();

            unselectedCoverageFiles.Should().HaveCount(2);
            foreach (var unselectedCoverageFile in unselectedCoverageFiles)
            {
                result.InitialXmlByPath[unselectedCoverageFile].Should().Contain("""<SequencePoint vc="0" """);
                result.FinalXmlByPath[unselectedCoverageFile].Should().Contain("""<SequencePoint vc="1" """);
            }

            result.TestSession.Metrics.Should().Contain(new KeyValuePair<string, double>(CodeCoverageTags.PercentageOfTotalLines, 100));
            result.TestSession.Meta.Should().Contain(CodeCoverageTags.Backfilled, "true");
        }

        /// <summary>
        /// Verifies that overlapping lower-priority reports are rewritten independently when selected reports can be published.
        /// </summary>
        [Fact]
        public void CoverletCollectorXmlCoverageBackfillsOverlappingUnselectedReportsWhenSelectedReportsCanBeBackfilled()
        {
            var unselectedCoverageFiles = new List<string>();
            Action<string> writeOverlappingSelectedAndUnselectedReports = resultsDirectory =>
            {
                WriteCoverletCollectorCoverageFile(resultsDirectory, [SimplePassTestCoveredLine]);
                foreach (var selectedCoverageFile in Directory.GetFiles(resultsDirectory, "coverage.cobertura.xml", SearchOption.AllDirectories))
                {
                    var selectedAttachmentDirectory = Path.GetDirectoryName(selectedCoverageFile);
                    if (selectedAttachmentDirectory is null)
                    {
                        throw new InvalidOperationException("Could not resolve selected coverage attachment directory.");
                    }

                    unselectedCoverageFiles.Add(WriteCoverletCollectorOpenCoverCoverageFileInAttachmentDirectory(selectedAttachmentDirectory, [SimplePassTestCoveredLine]));
                }
            };

            var result = RunCoverletCollectorXmlCoverageScenario(
                () =>
                {
                    var backfillData = CreateCoverageBackfillData(XUnitSampleSourcePath, SimplePassTestCoveredLine);
                    CoverageBackfillDataStore.Persist(TestOptimization.Instance, backfillData);
                    CoverageBackfillDataStore.RecordActualItrSkip();
                    CoverageBackfillDataStore.RecordCoverageIpcFailure(nameof(CodeCoverageReportSource.Coverlet));
                },
                writeOverlappingSelectedAndUnselectedReports,
                SimplePassTestCoveredLine);

            using var scopeAssertions = new AssertionScope();

            unselectedCoverageFiles.Should().HaveCount(2);
            foreach (var unselectedCoverageFile in unselectedCoverageFiles)
            {
                result.InitialXmlByPath[unselectedCoverageFile].Should().Contain($"""<SequencePoint vc="0" uspid="{SimplePassTestCoveredLine}" ordinal="0" sl="{SimplePassTestCoveredLine}" """);
                result.FinalXmlByPath[unselectedCoverageFile].Should().Contain($"""<SequencePoint vc="1" uspid="{SimplePassTestCoveredLine}" ordinal="0" sl="{SimplePassTestCoveredLine}" """);
            }

            result.FinalXml.Should().Contain($"""<line number="{SimplePassTestCoveredLine}" hits="1" />""");
            result.TestSession.Metrics.Should().Contain(new KeyValuePair<string, double>(CodeCoverageTags.PercentageOfTotalLines, 100));
            result.TestSession.Meta.Should().Contain(CodeCoverageTags.Backfilled, "true");
        }

        /// <summary>
        /// Verifies that a lower-priority report in the same attachment directory is used when the preferred report cannot be processed.
        /// </summary>
        [Fact]
        public void CoverletCollectorXmlCoverageUsesLowerPriorityReportWhenSelectedReportCannotBeProcessed()
        {
            string lowerPriorityCoverageFile = null;
            Action<string> replaceSelectedReportAndWriteLowerPriorityReport = resultsDirectory =>
            {
                var selectedCoverageFiles = Directory.GetFiles(resultsDirectory, "coverage.cobertura.xml", SearchOption.AllDirectories);
                selectedCoverageFiles.Should().HaveCount(1);
                var selectedCoverageFile = selectedCoverageFiles[0];
                var selectedAttachmentDirectory = Path.GetDirectoryName(selectedCoverageFile);
                if (selectedAttachmentDirectory is null)
                {
                    throw new InvalidOperationException("Could not resolve selected coverage attachment directory.");
                }

                File.WriteAllText(selectedCoverageFile, "<coverage><broken></coverage>");
                lowerPriorityCoverageFile = WriteCoverletCollectorOpenCoverCoverageFileInAttachmentDirectory(selectedAttachmentDirectory, [SimplePassTestCoveredLine]);
            };

            var result = RunCoverletCollectorXmlCoverageScenario(
                () =>
                {
                    var backfillData = CreateCoverageBackfillData(XUnitSampleSourcePath, SimplePassTestCoveredLine);
                    CoverageBackfillDataStore.Persist(TestOptimization.Instance, backfillData);
                    CoverageBackfillDataStore.RecordActualItrSkip();
                    CoverageBackfillDataStore.RecordCoverageIpcFailure(nameof(CodeCoverageReportSource.Coverlet));
                },
                replaceSelectedReportAndWriteLowerPriorityReport,
                SimplePassTestCoveredLine);

            using var scopeAssertions = new AssertionScope();

            result.FinalXml.Should().Be(result.InitialXml);
            result.InitialXmlByPath[lowerPriorityCoverageFile].Should().Contain($"""<SequencePoint vc="0" uspid="{SimplePassTestCoveredLine}" ordinal="0" sl="{SimplePassTestCoveredLine}" """);
            result.FinalXmlByPath[lowerPriorityCoverageFile].Should().Contain($"""<SequencePoint vc="1" uspid="{SimplePassTestCoveredLine}" ordinal="0" sl="{SimplePassTestCoveredLine}" """);
            result.TestSession.Metrics.Should().Contain(new KeyValuePair<string, double>(CodeCoverageTags.PercentageOfTotalLines, 100));
            result.TestSession.Meta.Should().Contain(CodeCoverageTags.Backfilled, "true");
        }

        /// <summary>
        /// Verifies that an unselected lower-priority report does not block publication when selected reports can be backfilled safely.
        /// </summary>
        [Fact]
        public void CoverletCollectorXmlCoverageIgnoresUnselectedReportWhenSelectedReportsCanBeBackfilled()
        {
            string unselectedCoverageFile = null;
            Action<string> writeLowerPriorityReportInSelectedAttachment = resultsDirectory =>
            {
                var selectedCoverageFiles = Directory.GetFiles(resultsDirectory, "coverage.cobertura.xml", SearchOption.AllDirectories);
                selectedCoverageFiles.Should().HaveCount(1);
                var selectedCoverageFile = selectedCoverageFiles[0];
                var selectedAttachmentDirectory = Path.GetDirectoryName(selectedCoverageFile);
                if (selectedAttachmentDirectory is null)
                {
                    throw new InvalidOperationException("Could not resolve selected coverage attachment directory.");
                }

                unselectedCoverageFile = WriteCoverletCollectorOpenCoverCoverageFileInAttachmentDirectory(selectedAttachmentDirectory, [SimplePassTestCoveredLine], duplicateFirstLine: true);
            };

            var result = RunCoverletCollectorXmlCoverageScenario(
                () =>
                {
                    var backfillData = CreateCoverageBackfillData(XUnitSampleSourcePath, SimplePassTestCoveredLine);
                    CoverageBackfillDataStore.Persist(TestOptimization.Instance, backfillData);
                    CoverageBackfillDataStore.RecordActualItrSkip();
                    CoverageBackfillDataStore.RecordCoverageIpcFailure(nameof(CodeCoverageReportSource.Coverlet));
                },
                writeLowerPriorityReportInSelectedAttachment,
                SimplePassTestCoveredLine);

            using var scopeAssertions = new AssertionScope();

            result.FinalXml.Should().Contain($"""<line number="{SimplePassTestCoveredLine}" hits="1" />""");
            result.FinalXmlByPath[unselectedCoverageFile].Should().Be(result.InitialXmlByPath[unselectedCoverageFile]);
            result.TestSession.Metrics.Should().Contain(new KeyValuePair<string, double>(CodeCoverageTags.PercentageOfTotalLines, 100));
            result.TestSession.Meta.Should().Contain(CodeCoverageTags.Backfilled, "true");
        }

        /// <summary>
        /// Verifies that an unselected lower-priority report can be partially backfilled when backend coverage has extra lines.
        /// </summary>
        [Fact]
        public void CoverletCollectorXmlCoverageBackfillsUnselectedReportWhenBackendHasExtraLines()
        {
            var otherCoveredLine = 26;
            string unselectedCoverageFile = null;
            Action<string> writeLowerPriorityReportInSelectedAttachment = resultsDirectory =>
            {
                var selectedCoverageFiles = Directory.GetFiles(resultsDirectory, "coverage.cobertura.xml", SearchOption.AllDirectories);
                selectedCoverageFiles.Should().HaveCount(1);
                var selectedCoverageFile = selectedCoverageFiles[0];
                var selectedAttachmentDirectory = Path.GetDirectoryName(selectedCoverageFile);
                if (selectedAttachmentDirectory is null)
                {
                    throw new InvalidOperationException("Could not resolve selected coverage attachment directory.");
                }

                unselectedCoverageFile = WriteCoverletCollectorOpenCoverCoverageFileInAttachmentDirectory(selectedAttachmentDirectory, [SimplePassTestCoveredLine]);
            };

            var result = RunCoverletCollectorXmlCoverageScenario(
                () =>
                {
                    var backfillData = CreateCoverageBackfillData(XUnitSampleSourcePath, SimplePassTestCoveredLine, otherCoveredLine);
                    CoverageBackfillDataStore.Persist(TestOptimization.Instance, backfillData);
                    CoverageBackfillDataStore.RecordActualItrSkip();
                    CoverageBackfillDataStore.RecordCoverageIpcFailure(nameof(CodeCoverageReportSource.Coverlet));
                },
                writeLowerPriorityReportInSelectedAttachment,
                SimplePassTestCoveredLine,
                otherCoveredLine);

            using var scopeAssertions = new AssertionScope();

            result.FinalXml.Should().Contain($"""<line number="{SimplePassTestCoveredLine}" hits="1" />""");
            result.FinalXml.Should().Contain($"""<line number="{otherCoveredLine}" hits="1" />""");
            result.InitialXmlByPath[unselectedCoverageFile].Should().Contain($"""<SequencePoint vc="0" uspid="{SimplePassTestCoveredLine}" ordinal="0" sl="{SimplePassTestCoveredLine}" """);
            result.FinalXmlByPath[unselectedCoverageFile].Should().Contain($"""<SequencePoint vc="1" uspid="{SimplePassTestCoveredLine}" ordinal="0" sl="{SimplePassTestCoveredLine}" """);
            result.TestSession.Metrics.Should().Contain(new KeyValuePair<string, double>(CodeCoverageTags.PercentageOfTotalLines, 100));
            result.TestSession.Meta.Should().Contain(CodeCoverageTags.Backfilled, "true");
        }

        /// <summary>
        /// Verifies that selected Coverlet collector XML reports keep local coverage when backend coverage matches unsafe local candidates.
        /// </summary>
        [Fact]
        public void CoverletCollectorXmlCoveragePublishesLocalCoverageWhenSelectedReportsMatchSameBackendPathThroughDifferentLocalCandidates()
        {
            const string BackendSuffixPath = "test-applications/integrations/Samples.XUnitTests/TestSuite.cs";
            var result = RunCoverletCollectorXmlCoverageScenario(
                () =>
                {
                    var backfillData = CreateCoverageBackfillData(BackendSuffixPath, SimplePassTestCoveredLine);
                    CoverageBackfillDataStore.Persist(TestOptimization.Instance, backfillData);
                    CoverageBackfillDataStore.RecordActualItrSkip();
                    CoverageBackfillDataStore.RecordCoverageIpcFailure(nameof(CodeCoverageReportSource.Coverlet));
                },
                resultsDirectory => WriteCoverletCollectorCoverageFile(resultsDirectory, [SimplePassTestCoveredLine], sourcePath: $"alternate/{BackendSuffixPath}"),
                SimplePassTestCoveredLine);

            using var scopeAssertions = new AssertionScope();

            foreach (var initialXmlByPath in result.InitialXmlByPath)
            {
                result.FinalXmlByPath.Should().ContainKey(initialXmlByPath.Key);
                result.FinalXmlByPath[initialXmlByPath.Key].Should().Be(initialXmlByPath.Value);
            }

            result.FinalXml.Should().Contain($"""<line number="{SimplePassTestCoveredLine}" hits="0" />""");
            result.TestSession.Metrics.Should().Contain(new KeyValuePair<string, double>(CodeCoverageTags.PercentageOfTotalLines, 0));
            result.TestSession.Meta.Should().NotContainKey(CodeCoverageTags.Backfilled);
        }

        /// <summary>
        /// Verifies that a pre-existing Coverlet attachment is excluded even when its timestamp is refreshed during the current run.
        /// </summary>
        [Fact]
        public void CoverletCollectorXmlCoverageIgnoresPreExistingAttachmentTouchedDuringRun()
        {
            string staleCoverageFile = null;
            var result = RunCoverletCollectorXmlCoverageScenarioCore(
                () =>
                {
                    var backfillData = CreateCoverageBackfillData(XUnitSampleSourcePath, SimplePassTestCoveredLine);
                    CoverageBackfillDataStore.Persist(TestOptimization.Instance, backfillData);
                    CoverageBackfillDataStore.RecordActualItrSkip();
                    CoverageBackfillDataStore.RecordCoverageIpcFailure(nameof(CodeCoverageReportSource.Coverlet));
                },
                configureSession: null,
                configureResultsDirectoryBeforeSession: resultsDirectory =>
                {
                    staleCoverageFile = WriteCoverletCollectorCoverageFile(resultsDirectory, [SimplePassTestCoveredLine], sourcePath: "stale/Unrelated.cs");
                    File.SetLastWriteTimeUtc(staleCoverageFile, DateTime.UtcNow.AddMinutes(-5));
                },
                configureResultsDirectory: _ => File.SetLastWriteTimeUtc(staleCoverageFile, DateTime.UtcNow),
                useOpenCoverReport: false,
                uncoveredLines: [SimplePassTestCoveredLine]);

            using var scopeAssertions = new AssertionScope();

            result.FinalXml.Should().Contain($"""<line number="{SimplePassTestCoveredLine}" hits="1" />""");
            result.TestSession.Metrics.Should().Contain(new KeyValuePair<string, double>(CodeCoverageTags.PercentageOfTotalLines, 100));
            result.TestSession.Meta.Should().Contain(CodeCoverageTags.Backfilled, "true");
            result.InitialXmlByPath.Should().ContainKey(staleCoverageFile);
            result.FinalXmlByPath.Should().ContainKey(staleCoverageFile);
            result.FinalXmlByPath[staleCoverageFile].Should().Be(result.InitialXmlByPath[staleCoverageFile]);
        }

        /// <summary>
        /// Verifies that timestamp granularity does not hide a current-session Coverlet attachment that was not present in the baseline.
        /// </summary>
        [Fact]
        public void CoverletCollectorXmlCoverageIncludesAttachmentTimestampJustBeforeSessionStartWhenNotInBaseline()
        {
            string currentCoverageFile = null;
            var result = RunCoverletCollectorXmlCoverageScenario(
                () =>
                {
                    var backfillData = CreateCoverageBackfillData(XUnitSampleSourcePath, SimplePassTestCoveredLine);
                    CoverageBackfillDataStore.Persist(TestOptimization.Instance, backfillData);
                    CoverageBackfillDataStore.RecordActualItrSkip();
                    CoverageBackfillDataStore.RecordCoverageIpcFailure(nameof(CodeCoverageReportSource.Coverlet));
                },
                configureSession: session =>
                {
                    var roundedBeforeSessionStart = session.StartTime.UtcDateTime.AddMilliseconds(-100);
                    File.SetCreationTimeUtc(currentCoverageFile, roundedBeforeSessionStart);
                    File.SetLastWriteTimeUtc(currentCoverageFile, roundedBeforeSessionStart);
                },
                configureResultsDirectory: resultsDirectory =>
                {
                    currentCoverageFile = Directory.GetFiles(resultsDirectory, "coverage.cobertura.xml", SearchOption.AllDirectories)[0];
                },
                useOpenCoverReport: false,
                uncoveredLines: [SimplePassTestCoveredLine]);

            using var scopeAssertions = new AssertionScope();

            result.FinalXml.Should().Contain($"""<line number="{SimplePassTestCoveredLine}" hits="1" />""");
            result.TestSession.Metrics.Should().Contain(new KeyValuePair<string, double>(CodeCoverageTags.PercentageOfTotalLines, 100));
            result.TestSession.Meta.Should().Contain(CodeCoverageTags.Backfilled, "true");
        }

        /// <summary>
        /// Runs one synthetic Coverlet collector fallback scenario through the runner finalizer.
        /// </summary>
        /// <param name="configureBackfill">Callback that persists backend coverage and actual-skip state before session finalization.</param>
        /// <param name="uncoveredLines">One-based source lines emitted as uncovered executable lines in the synthetic Coverlet XML attachment.</param>
        /// <returns>Captured XML contents and CI Visibility session data from the completed runner command.</returns>
        private CoverletCollectorXmlCoverageScenarioResult RunCoverletCollectorXmlCoverageScenario(Action configureBackfill, params int[] uncoveredLines)
        {
            return RunCoverletCollectorXmlCoverageScenario(configureBackfill, configureSession: null, uncoveredLines);
        }

        /// <summary>
        /// Runs one synthetic Coverlet collector fallback scenario through the runner finalizer.
        /// </summary>
        /// <param name="configureBackfill">Callback that persists backend coverage and actual-skip state before session finalization.</param>
        /// <param name="configureResultsDirectory">Optional callback that writes additional current-session Coverlet attachments.</param>
        /// <param name="uncoveredLines">One-based source lines emitted as uncovered executable lines in the primary synthetic Coverlet XML attachment.</param>
        /// <returns>Captured XML contents and CI Visibility session data from the completed runner command.</returns>
        private CoverletCollectorXmlCoverageScenarioResult RunCoverletCollectorXmlCoverageScenario(Action configureBackfill, Action<string> configureResultsDirectory, params int[] uncoveredLines)
        {
            return RunCoverletCollectorXmlCoverageScenario(configureBackfill, configureSession: null, configureResultsDirectory, uncoveredLines);
        }

        /// <summary>
        /// Runs one synthetic Coverlet collector fallback scenario through the runner finalizer.
        /// </summary>
        /// <param name="configureBackfill">Callback that persists backend coverage and actual-skip state before session finalization.</param>
        /// <param name="configureSession">Optional callback that records pre-existing session coverage before finalization.</param>
        /// <param name="uncoveredLines">One-based source lines emitted as uncovered executable lines in the synthetic Coverlet XML attachment.</param>
        /// <returns>Captured XML contents and CI Visibility session data from the completed runner command.</returns>
        private CoverletCollectorXmlCoverageScenarioResult RunCoverletCollectorXmlCoverageScenario(Action configureBackfill, Action<TestSession> configureSession, params int[] uncoveredLines)
        {
            return RunCoverletCollectorXmlCoverageScenario(configureBackfill, configureSession, configureResultsDirectory: null, uncoveredLines);
        }

        /// <summary>
        /// Runs one synthetic Coverlet collector fallback scenario through the runner finalizer.
        /// </summary>
        /// <param name="configureBackfill">Callback that persists backend coverage and actual-skip state before session finalization.</param>
        /// <param name="configureSession">Optional callback that records pre-existing session coverage before finalization.</param>
        /// <param name="configureResultsDirectory">Optional callback that writes additional current-session Coverlet attachments.</param>
        /// <param name="uncoveredLines">One-based source lines emitted as uncovered executable lines in the primary synthetic Coverlet XML attachment.</param>
        /// <returns>Captured XML contents and CI Visibility session data from the completed runner command.</returns>
        private CoverletCollectorXmlCoverageScenarioResult RunCoverletCollectorXmlCoverageScenario(Action configureBackfill, Action<TestSession> configureSession, Action<string> configureResultsDirectory, params int[] uncoveredLines)
        {
            return RunCoverletCollectorXmlCoverageScenario(configureBackfill, configureSession, configureResultsDirectory, useOpenCoverReport: false, uncoveredLines);
        }

        /// <summary>
        /// Runs one synthetic Coverlet collector fallback scenario through the runner finalizer.
        /// </summary>
        /// <param name="configureBackfill">Callback that persists backend coverage and actual-skip state before session finalization.</param>
        /// <param name="useOpenCoverReport">Whether the primary synthetic attachment should use Coverlet's OpenCover XML format.</param>
        /// <param name="uncoveredLines">One-based source lines emitted as uncovered executable lines in the primary synthetic Coverlet XML attachment.</param>
        /// <returns>Captured XML contents and CI Visibility session data from the completed runner command.</returns>
        private CoverletCollectorXmlCoverageScenarioResult RunCoverletCollectorXmlCoverageScenario(Action configureBackfill, bool useOpenCoverReport, params int[] uncoveredLines)
        {
            return RunCoverletCollectorXmlCoverageScenario(configureBackfill, configureSession: null, configureResultsDirectory: null, useOpenCoverReport, uncoveredLines);
        }

        /// <summary>
        /// Runs one synthetic Coverlet collector fallback scenario through the runner finalizer.
        /// </summary>
        /// <param name="configureBackfill">Callback that persists backend coverage and actual-skip state before session finalization.</param>
        /// <param name="configureSession">Optional callback that records pre-existing session coverage before finalization.</param>
        /// <param name="configureResultsDirectory">Optional callback that writes additional current-session Coverlet attachments.</param>
        /// <param name="useOpenCoverReport">Whether the primary synthetic attachment should use Coverlet's OpenCover XML format.</param>
        /// <param name="uncoveredLines">One-based source lines emitted as uncovered executable lines in the primary synthetic Coverlet XML attachment.</param>
        /// <returns>Captured XML contents and CI Visibility session data from the completed runner command.</returns>
        private CoverletCollectorXmlCoverageScenarioResult RunCoverletCollectorXmlCoverageScenario(Action configureBackfill, Action<TestSession> configureSession, Action<string> configureResultsDirectory, bool useOpenCoverReport, params int[] uncoveredLines)
        {
            return RunCoverletCollectorXmlCoverageScenarioCore(configureBackfill, configureSession, configureResultsDirectoryBeforeSession: null, configureResultsDirectory, useOpenCoverReport, uncoveredLines);
        }

        /// <summary>
        /// Runs one synthetic Coverlet collector fallback scenario through the runner finalizer.
        /// </summary>
        /// <param name="configureBackfill">Callback that persists backend coverage and actual-skip state before session finalization.</param>
        /// <param name="configureSession">Optional callback that records pre-existing session coverage before finalization.</param>
        /// <param name="configureResultsDirectoryBeforeSession">Optional callback that writes pre-existing Coverlet attachments before the child session starts.</param>
        /// <param name="configureResultsDirectory">Optional callback that writes additional current-session Coverlet attachments.</param>
        /// <param name="useOpenCoverReport">Whether the primary synthetic attachment should use Coverlet's OpenCover XML format.</param>
        /// <param name="uncoveredLines">One-based source lines emitted as uncovered executable lines in the primary synthetic Coverlet XML attachment.</param>
        /// <returns>Captured XML contents and CI Visibility session data from the completed runner command.</returns>
        private CoverletCollectorXmlCoverageScenarioResult RunCoverletCollectorXmlCoverageScenarioCore(Action configureBackfill, Action<TestSession> configureSession, Action<string> configureResultsDirectoryBeforeSession, Action<string> configureResultsDirectory, bool useOpenCoverReport, int[] uncoveredLines)
        {
            PrepareRunnerSettingsInputs();
            TestOptimization.Instance.Reset();
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.DebugEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestsSkippingEnabled, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.KnownTestsEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.DynamicInstrumentationEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ImpactedTestsDetectionEnabled, "0");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestManagementEnabled, "0");

            string command = null;
            string arguments = null;
            Dictionary<string, string> environmentVariables = null;
            Dictionary<string, string> originalEnvironmentVariables = null;
            bool callbackInvoked = false;
            MockCIVisibilityTestModule testSession = null;

            using var coverageResultsDirectory = new TemporaryDirectory("dd-ci-coverlet-collector-");
            string coverageFile = null;
            string initialXml = null;
            var initialXmlByPath = new Dictionary<string, string>();
            var finalXmlByPath = new Dictionary<string, string>();
            var backfillRunFolder = Path.Combine(coverageResultsDirectory.RootPath, ".dd-backfill");
            const string StaleRunId = "stale-run-id";
            var staleBackfillCommand = "stale dotnet test";
            var staleSessionWorkingDirectory = Path.Combine(coverageResultsDirectory.RootPath, "stale-working-directory");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestOptimizationRunId, StaleRunId);
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestSessionCommand, staleBackfillCommand);
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, staleSessionWorkingDirectory);
            EnvironmentHelpers.SetEnvironmentVariable(
                Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder,
                backfillRunFolder);
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, "1");
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, staleBackfillCommand);
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, Path.Combine(coverageResultsDirectory.RootPath, "stale-coverage-backfill.json"));
            var testSessionCommandPrefix = $"dotnet test --collect:coverlet.collector --ResultsDirectory:{coverageResultsDirectory.RootPath}";

            try
            {
                Program.CallbackForTests = (c, a, e) =>
                {
                    command = c;
                    arguments = a;
                    environmentVariables = e;
                    callbackInvoked = true;
                    originalEnvironmentVariables = new Dictionary<string, string>();
                    foreach (var environmentVariable in e)
                    {
                        originalEnvironmentVariables[environmentVariable.Key] = EnvironmentHelpers.GetEnvironmentVariable(environmentVariable.Key);
                        EnvironmentHelpers.SetEnvironmentVariable(environmentVariable.Key, environmentVariable.Value);
                    }

                    // The callback runs the simulated child testhost in the same process as the runner. Reset the command-line
                    // cache after applying the child environment so coverage capability checks observe the same inputs they
                    // would see in a real child process.
                    CoverageBackfillCapability.ResetCommandLineCacheForTests();

                    configureResultsDirectoryBeforeSession?.Invoke(coverageResultsDirectory.RootPath);
                    var session = DotnetCommon.CreateSession();
                    coverageFile = useOpenCoverReport ?
                                       WriteCoverletCollectorOpenCoverCoverageFile(coverageResultsDirectory.RootPath, uncoveredLines) :
                                       WriteCoverletCollectorCoverageFile(coverageResultsDirectory.RootPath, uncoveredLines);
                    configureResultsDirectory?.Invoke(coverageResultsDirectory.RootPath);
                    foreach (var xmlReport in Directory.GetFiles(coverageResultsDirectory.RootPath, "coverage.*.xml", SearchOption.AllDirectories))
                    {
                        initialXmlByPath[xmlReport] = File.ReadAllText(xmlReport);
                    }

                    initialXml = File.ReadAllText(coverageFile);
                    configureBackfill();
                    configureSession?.Invoke(session);
                    DotnetCommon.FinalizeSession(session, 0, null);
                };

                using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
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

                var agentUrl = $"http://localhost:{agent.Port}";
                var commandLine = $"{CommandPrefix} {testSessionCommandPrefix} --dd-env TestEnv --dd-service TestService --dd-version TestVersion --tracer-home TestTracerHome --agent-url {agentUrl}";

                using var console = ConsoleHelper.Redirect();

                var exitCode = Program.Main(commandLine.Split(' '));
                var finalXml = coverageFile is null ? string.Empty : File.ReadAllText(coverageFile);
                foreach (var xmlReport in initialXmlByPath.Keys)
                {
                    finalXmlByPath[xmlReport] = File.ReadAllText(xmlReport);
                }

                using var scope = new AssertionScope();

                scope.AddReportable("output", console.Output);
                scope.AddReportable("coverageFile", coverageFile);
                scope.AddReportable("initialCoverageXml", initialXml ?? string.Empty);
                scope.AddReportable("finalCoverageXml", finalXml);

                exitCode.Should().Be(0);
                callbackInvoked.Should().BeTrue();
                command.Should().Be("dotnet");
                arguments.Should().StartWith($"test --collect:coverlet.collector --ResultsDirectory:{coverageResultsDirectory.RootPath}");
                arguments.Should().Contain("--collect DatadogCoverage");
                environmentVariables.Should().NotBeNull();
                environmentVariables.Should().ContainKey(Configuration.ConfigurationKeys.CIVisibility.TestOptimizationRunId);
                environmentVariables[Configuration.ConfigurationKeys.CIVisibility.TestOptimizationRunId].Should().NotBe(StaleRunId);
                environmentVariables.Should().Contain(
                    Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder,
                    Path.Combine(Environment.CurrentDirectory, ".dd", environmentVariables[Configuration.ConfigurationKeys.CIVisibility.TestOptimizationRunId]));
                environmentVariables[Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder].Should().NotBe(backfillRunFolder);
                environmentVariables.Should().Contain(Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, string.Empty);
                environmentVariables.Should().Contain(Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, string.Empty);
                environmentVariables.Should().Contain(Configuration.ConfigurationKeys.CIVisibility.TestSessionCommand, Environment.CommandLine);
                environmentVariables.Should().Contain(Configuration.ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, Environment.CurrentDirectory);
                environmentVariables[Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand].Should().StartWith(testSessionCommandPrefix);
                environmentVariables[Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand].Should().Contain("--collect DatadogCoverage");

                coverageFile.Should().NotBeNull();
                initialXml.Should().NotBeNull();
                testSession.Should().NotBeNull();

                return new CoverletCollectorXmlCoverageScenarioResult(initialXml, finalXml, testSession, initialXmlByPath, finalXmlByPath);
            }
            finally
            {
                if (originalEnvironmentVariables is not null)
                {
                    foreach (var environmentVariable in originalEnvironmentVariables)
                    {
                        EnvironmentHelpers.SetEnvironmentVariable(environmentVariable.Key, environmentVariable.Value);
                    }
                }

                Program.CallbackForTests = null;
                CoverageBackfillCapability.ResetCommandLineCacheForTests();
                TestOptimization.Instance.Reset();
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
        /// Reads the runner-owned response file represented by the final child-process argument string.
        /// </summary>
        /// <param name="arguments">Rendered child-process argument string.</param>
        /// <returns>Response file contents.</returns>
        private string ReadSingleResponseFileArgument(string arguments)
        {
            var responseFileReference = arguments.Trim().Trim('"');
            responseFileReference.Should().StartWith("@");
            var responseFilePath = responseFileReference.Substring(1);
            File.Exists(responseFilePath).Should().BeTrue();
            return File.ReadAllText(responseFilePath);
        }

        private int CountArgument(string arguments, string expectedArgument)
        {
            var count = 0;
            foreach (var argument in arguments.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (argument.Equals(expectedArgument, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
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
        /// Restores cached CI environment values mutated by runner tests.
        /// </summary>
        private void RestoreCachedCiEnvironmentValues()
        {
            SetCachedCiEnvironmentValue(nameof(CIEnvironmentValues.Commit), _previousCachedCiCommit);
            SetCachedCiEnvironmentValue(nameof(CIEnvironmentValues.Repository), _previousCachedCiRepository);
            SetCachedCiEnvironmentValue(nameof(CIEnvironmentValues.Branch), _previousCachedCiBranch);
            SetCachedCiEnvironmentValue(nameof(CIEnvironmentValues.WorkspacePath), _previousCachedCiWorkspacePath);
        }

        /// <summary>
        /// Creates a minimal Coverlet collector Cobertura attachment with the skipped test line marked as uncovered.
        /// </summary>
        /// <param name="resultsDirectory">VSTest results directory where Coverlet writes attachment subdirectories.</param>
        /// <param name="uncoveredLines">One-based source lines that the report should expose as executable but uncovered.</param>
        /// <param name="sourcePath">Source path written in the Cobertura class entry.</param>
        /// <param name="coverageFile">Optional absolute path to the generated Cobertura report.</param>
        /// <returns>Absolute path to the generated Cobertura report.</returns>
        private string WriteCoverletCollectorCoverageFile(string resultsDirectory, int[] uncoveredLines, string sourcePath = XUnitSampleSourcePath, string coverageFile = null)
        {
            if (coverageFile is null)
            {
                var attachmentDirectory = Path.Combine(resultsDirectory, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(attachmentDirectory);
                coverageFile = Path.Combine(attachmentDirectory, "coverage.cobertura.xml");
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(coverageFile));
            }

            var lineEntries = new List<string>();
            foreach (var line in uncoveredLines)
            {
                lineEntries.Add($"""                            <line number="{line}" hits="0" />""");
            }

            var lineCount = uncoveredLines.Length;
            var coverageXml =
                $"""
                 <coverage line-rate="0" lines-valid="{lineCount}" lines-covered="0">
                   <packages>
                     <package name="sample" line-rate="0">
                       <classes>
                         <class name="Samples.XUnitTests.TestSuite" filename="{sourcePath}" line-rate="0">
                           <lines>
                 {string.Join(Environment.NewLine, lineEntries)}
                           </lines>
                         </class>
                       </classes>
                     </package>
                   </packages>
                 </coverage>
                 """;
            File.WriteAllText(coverageFile, coverageXml);
            return coverageFile;
        }

        private void CorruptCoverletCollectorCoverageFiles(string resultsDirectory)
        {
            foreach (var coverageFile in Directory.GetFiles(resultsDirectory, "coverage.cobertura.xml", SearchOption.AllDirectories))
            {
                File.WriteAllText(coverageFile, "<coverage");
            }
        }

        /// <summary>
        /// Creates a minimal Coverlet collector OpenCover attachment with skipped test lines marked as uncovered.
        /// </summary>
        /// <param name="resultsDirectory">VSTest results directory where Coverlet writes attachment subdirectories.</param>
        /// <param name="uncoveredLines">One-based source lines that the report should expose as executable but uncovered.</param>
        /// <returns>Absolute path to the generated OpenCover report.</returns>
        private string WriteCoverletCollectorOpenCoverCoverageFile(string resultsDirectory, int[] uncoveredLines, string sourcePath = XUnitSampleSourcePath)
        {
            var attachmentDirectory = Path.Combine(resultsDirectory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(attachmentDirectory);
            return WriteCoverletCollectorOpenCoverCoverageFileInAttachmentDirectory(attachmentDirectory, uncoveredLines, sourcePath);
        }

        /// <summary>
        /// Creates a minimal Coverlet collector OpenCover attachment inside an existing VSTest attachment directory.
        /// </summary>
        /// <param name="attachmentDirectory">Existing VSTest attachment directory.</param>
        /// <param name="uncoveredLines">One-based source lines that the report should expose as executable but uncovered.</param>
        /// <param name="sourcePath">Source path written in the OpenCover file entry.</param>
        /// <param name="duplicateFirstLine">Whether to emit a duplicate sequence point for the first line.</param>
        /// <returns>Absolute path to the generated OpenCover report.</returns>
        private string WriteCoverletCollectorOpenCoverCoverageFileInAttachmentDirectory(string attachmentDirectory, int[] uncoveredLines, string sourcePath = XUnitSampleSourcePath, bool duplicateFirstLine = false)
        {
            var coverageFile = Path.Combine(attachmentDirectory, "coverage.opencover.xml");
            var sequencePoints = new List<string>();
            var ordinal = 0;
            foreach (var line in uncoveredLines)
            {
                sequencePoints.Add($"""                <SequencePoint vc="0" uspid="{line}" ordinal="{ordinal}" sl="{line}" sc="1" el="{line}" ec="2" fileid="1" />""");
                ordinal++;
            }

            if (duplicateFirstLine && uncoveredLines.Length > 0)
            {
                var line = uncoveredLines[0];
                sequencePoints.Add($"""                <SequencePoint vc="0" uspid="{line}" ordinal="{ordinal}" sl="{line}" sc="1" el="{line}" ec="2" fileid="1" />""");
            }

            var lineCount = uncoveredLines.Length;
            var coverageXml =
                $"""
                 <CoverageSession>
                   <Summary numSequencePoints="{lineCount}" visitedSequencePoints="0" sequenceCoverage="0" />
                   <Modules>
                     <Module>
                       <Files>
                         <File uid="1" fullPath="{sourcePath}" />
                       </Files>
                       <Classes>
                         <Class>
                           <Summary numSequencePoints="{lineCount}" visitedSequencePoints="0" sequenceCoverage="0" />
                           <Methods>
                             <Method>
                               <Summary numSequencePoints="{lineCount}" visitedSequencePoints="0" sequenceCoverage="0" />
                               <FileRef uid="1" />
                               <SequencePoints>
                 {string.Join(Environment.NewLine, sequencePoints)}
                               </SequencePoints>
                             </Method>
                           </Methods>
                         </Class>
                       </Classes>
                     </Module>
                   </Modules>
                 </CoverageSession>
                 """;
            File.WriteAllText(coverageFile, coverageXml);
            return coverageFile;
        }

        /// <summary>
        /// Creates backend coverage data with covered lines encoded using the backend bitmap format.
        /// </summary>
        /// <param name="path">Repository-relative source path reported by the backend.</param>
        /// <param name="lines">One-based covered source lines.</param>
        /// <returns>Decoded backend coverage data for the supplied source lines.</returns>
        private CoverageBackfillData CreateCoverageBackfillData(string path, params int[] lines)
        {
            var maxLine = 0;
            foreach (var line in lines)
            {
                if (line > maxLine)
                {
                    maxLine = line;
                }
            }

            var bitmap = new byte[(maxLine + 7) / 8];
            foreach (var line in lines)
            {
                var index = line - 1;
                bitmap[index >> 3] |= (byte)(128 >> (index & 7));
            }

            return CoverageBackfillData.FromBackendCoverage(
                new Dictionary<string, string>
                {
                    [path] = Convert.ToBase64String(bitmap)
                });
        }

        /// <summary>
        /// Creates a minimal Datadog internal global coverage payload with one executable but uncovered file.
        /// </summary>
        /// <param name="path">Local coverage file path.</param>
        /// <returns>Global coverage payload for the supplied file.</returns>
        private GlobalCoverageInfo CreateGlobalCoverage(string path)
        {
            return new GlobalCoverageInfo
            {
                Components =
                {
                    new ComponentCoverageInfo("Component")
                    {
                        Files =
                        {
                            new FileCoverageInfo(path)
                            {
                                ExecutableBitmap = [0b_1000_0000],
                                ExecutedBitmap = [0b_0000_0000]
                            }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Updates the cached CI environment used by Test Optimization when another inherited runner test initialized it first.
        /// </summary>
        private void SetCachedCiEnvironmentValue(string propertyName, string value)
        {
            typeof(CIEnvironmentValues).GetProperty(propertyName)?.SetValue(CIEnvironmentValues.Instance, value);
        }

        /// <summary>
        /// Captures the observable output from one Coverlet collector XML fallback scenario.
        /// </summary>
        private readonly struct CoverletCollectorXmlCoverageScenarioResult
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="CoverletCollectorXmlCoverageScenarioResult"/> struct.
            /// </summary>
            /// <param name="initialXml">XML contents before the runner finalizer applies coverage backfill.</param>
            /// <param name="finalXml">XML contents after the runner command completes.</param>
            /// <param name="testSession">Captured CI Visibility test-session event.</param>
            /// <param name="initialXmlByPath">XML contents by report path before the runner finalizer applies coverage backfill.</param>
            /// <param name="finalXmlByPath">XML contents by report path after the runner command completes.</param>
            public CoverletCollectorXmlCoverageScenarioResult(string initialXml, string finalXml, MockCIVisibilityTestModule testSession, IReadOnlyDictionary<string, string> initialXmlByPath, IReadOnlyDictionary<string, string> finalXmlByPath)
            {
                InitialXml = initialXml;
                FinalXml = finalXml;
                TestSession = testSession;
                InitialXmlByPath = initialXmlByPath;
                FinalXmlByPath = finalXmlByPath;
            }

            /// <summary>
            /// Gets the XML contents before the runner finalizer applies coverage backfill.
            /// </summary>
            public string InitialXml { get; }

            /// <summary>
            /// Gets the XML contents after the runner command completes.
            /// </summary>
            public string FinalXml { get; }

            /// <summary>
            /// Gets the captured CI Visibility test-session event.
            /// </summary>
            public MockCIVisibilityTestModule TestSession { get; }

            /// <summary>
            /// Gets the XML contents by report path before the runner finalizer applies coverage backfill.
            /// </summary>
            public IReadOnlyDictionary<string, string> InitialXmlByPath { get; }

            /// <summary>
            /// Gets the XML contents by report path after the runner command completes.
            /// </summary>
            public IReadOnlyDictionary<string, string> FinalXmlByPath { get; }
        }

        /// <summary>
        /// Owns a temporary directory and removes it when the test finishes.
        /// </summary>
        private sealed class TemporaryDirectory : IDisposable
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="TemporaryDirectory"/> class.
            /// </summary>
            /// <param name="prefix">Directory name prefix used to identify the test artifact.</param>
            public TemporaryDirectory(string prefix)
            {
                RootPath = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(RootPath);
            }

            /// <summary>
            /// Gets the absolute path of the owned temporary directory.
            /// </summary>
            public string RootPath { get; }

            /// <summary>
            /// Deletes the owned temporary directory when it still exists.
            /// </summary>
            public void Dispose()
            {
                if (Directory.Exists(RootPath))
                {
                    Directory.Delete(RootPath, recursive: true);
                }
            }
        }
    }
}
