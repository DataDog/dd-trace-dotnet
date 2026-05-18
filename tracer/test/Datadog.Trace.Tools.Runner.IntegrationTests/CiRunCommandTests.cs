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
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Backfill;
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
        Configuration.ConfigurationKeys.CIVisibility.GitRepositoryUrl)]
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
        /// Verifies that backend coverage data is ignored when no test was actually skipped by ITR.
        /// </summary>
        [Fact]
        public void CoverletCollectorXmlCoverageDoesNotBackfillWithoutActualItrSkip()
        {
            var result = RunCoverletCollectorXmlCoverageScenario(
                () =>
                {
                    CoverageBackfillDataStore.Persist(TestOptimization.Instance, CreateCoverageBackfillData(XUnitSampleSourcePath, SimplePassTestCoveredLine));
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
        /// Verifies that the runner fails closed when skipped-test backend coverage cannot be matched to the Coverlet XML source path.
        /// </summary>
        [Fact]
        public void CoverletCollectorXmlCoverageFailsClosedWhenBackendPathDoesNotMatch()
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
            result.TestSession.Metrics.Should().NotContainKey(CodeCoverageTags.PercentageOfTotalLines);
            result.TestSession.Meta.Should().NotContainKey(CodeCoverageTags.Backfilled);
        }

        /// <summary>
        /// Runs one synthetic Coverlet collector fallback scenario through the runner finalizer.
        /// </summary>
        /// <param name="configureBackfill">Callback that persists backend coverage and actual-skip state before session finalization.</param>
        /// <param name="uncoveredLines">One-based source lines emitted as uncovered executable lines in the synthetic Coverlet XML attachment.</param>
        /// <returns>Captured XML contents and CI Visibility session data from the completed runner command.</returns>
        private CoverletCollectorXmlCoverageScenarioResult RunCoverletCollectorXmlCoverageScenario(Action configureBackfill, params int[] uncoveredLines)
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
            var backfillRunFolder = Path.Combine(coverageResultsDirectory.RootPath, ".dd-backfill");
            EnvironmentHelpers.SetEnvironmentVariable(
                Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder,
                backfillRunFolder);
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

                    var session = DotnetCommon.CreateSession();
                    coverageFile = WriteCoverletCollectorCoverageFile(coverageResultsDirectory.RootPath, uncoveredLines);
                    initialXml = File.ReadAllText(coverageFile);
                    configureBackfill();
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
                environmentVariables.Should().Contain(Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, backfillRunFolder);
                environmentVariables.Should().Contain(Configuration.ConfigurationKeys.CIVisibility.TestSessionCommand, Environment.CommandLine);
                environmentVariables.Should().Contain(Configuration.ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, Environment.CurrentDirectory);
                environmentVariables[Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand].Should().StartWith(testSessionCommandPrefix);
                environmentVariables[Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand].Should().Contain("--collect DatadogCoverage");

                coverageFile.Should().NotBeNull();
                initialXml.Should().NotBeNull();
                testSession.Should().NotBeNull();

                return new CoverletCollectorXmlCoverageScenarioResult(initialXml, finalXml, testSession);
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
        /// Creates a minimal Coverlet collector Cobertura attachment with the skipped test line marked as uncovered.
        /// </summary>
        /// <param name="resultsDirectory">VSTest results directory where Coverlet writes attachment subdirectories.</param>
        /// <param name="uncoveredLines">One-based source lines that the report should expose as executable but uncovered.</param>
        /// <returns>Absolute path to the generated Cobertura report.</returns>
        private string WriteCoverletCollectorCoverageFile(string resultsDirectory, int[] uncoveredLines)
        {
            var attachmentDirectory = Path.Combine(resultsDirectory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(attachmentDirectory);
            var coverageFile = Path.Combine(attachmentDirectory, "coverage.cobertura.xml");
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
                         <class name="Samples.XUnitTests.TestSuite" filename="integrations/Samples.XUnitTests/TestSuite.cs" line-rate="0">
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

        /// <summary>
        /// Creates backend coverage data with one covered line encoded using the backend bitmap format.
        /// </summary>
        /// <param name="path">Repository-relative source path reported by the backend.</param>
        /// <param name="line">One-based covered source line.</param>
        /// <returns>Decoded backend coverage data for the supplied source line.</returns>
        private CoverageBackfillData CreateCoverageBackfillData(string path, int line)
        {
            var bitmap = new byte[(line + 7) / 8];
            var index = line - 1;
            bitmap[index >> 3] = (byte)(128 >> (index & 7));
            return CoverageBackfillData.FromBackendCoverage(
                new Dictionary<string, string>
                {
                    [path] = Convert.ToBase64String(bitmap)
                });
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
            public CoverletCollectorXmlCoverageScenarioResult(string initialXml, string finalXml, MockCIVisibilityTestModule testSession)
            {
                InitialXml = initialXml;
                FinalXml = finalXml;
                TestSession = testSession;
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
