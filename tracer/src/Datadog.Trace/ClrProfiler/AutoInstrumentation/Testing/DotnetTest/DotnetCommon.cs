// <copyright file="DotnetCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Ci.Ipc;
using Datadog.Trace.Ci.Ipc.Messages;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest
{
    internal static class DotnetCommon
    {
        internal const string DotnetTestIntegrationName = nameof(IntegrationId.DotnetTest);
        internal const IntegrationId DotnetTestIntegrationId = Configuration.IntegrationId.DotnetTest;

        private const int CoverageXmlRestoreMaxAttempts = 3;
        private const int CoverageXmlRestoreRetryDelayMilliseconds = 50;
        private const string VSTestArtifactsPostprocessArgument = "--artifactsProcessingMode-postprocess";

        internal static readonly IDatadogLogger Log = TestOptimization.Instance.Log;

        /// <summary>
        /// VSTest result-directory option names accepted by dotnet test and dotnet vstest command lines.
        /// </summary>
        private static readonly string[] CoverletResultsDirectoryOptions = ["--results-directory", "--ResultsDirectory", "/ResultsDirectory"];
        private static readonly char[] CommandLineQuoteCharacters = [' ', '\t', '\r', '\n', '"'];

        /// <summary>
        /// Coverlet collector XML attachments supported by the runner fallback, ordered by publication preference.
        /// </summary>
        private static readonly CoverletCollectorXmlReportDescriptor[] CoverletCollectorXmlReports =
        [
            new("coverage.cobertura.xml", CoverletCollectorXmlReportFormat.Cobertura, publishPriority: 0),
            new("coverage.opencover.xml", CoverletCollectorXmlReportFormat.OpenCover, publishPriority: 1)
        ];

        private static readonly TimeSpan CoverletCollectorXmlReportTimestampTolerance = TimeSpan.FromSeconds(1);
        private static readonly object CoverletCollectorXmlReportBaselineLock = new();
        private static readonly StringComparer CoverageReportPathComparer = FrameworkDescription.Instance.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        private static bool? _isDataCollectorDomainCache;
        private static bool? _isMsBuildTaskCache;
        private static CoverletCollectorXmlReportBaseline? _coverletCollectorXmlReportBaseline;
        private static bool _injectedSessionCoverletXmlFallbackEnabled;

        private delegate bool TryProcessCoverageXmlReport(CoverletCollectorXmlReport coverageReport, ExternalCoverageXmlBackfill.CoverageBackfillValidationState validationState, out ExternalCoverageXmlResult result);

        private delegate void RecordCoverageXmlResult(IReadOnlyList<CoverletCollectorXmlReport> coverageReports, ExternalCoverageXmlResult result, bool backfillValidated);

        internal delegate CoverletCollectorXmlProcessingResult ProcessCoverletCollectorXmlReports(TestSession session);

        private enum CoverletCollectorXmlReportFormat
        {
            Cobertura,
            OpenCover
        }

        private enum CoverageBackfillAvailability
        {
            NotRequired,
            Available,
            Unavailable
        }

        internal enum CoverletCollectorXmlProcessingResult
        {
            NotApplicable,
            Processed,
            FailedClosed
        }

        internal static bool DotnetTestIntegrationEnabled => TestOptimization.Instance.IsRunning && Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(DotnetTestIntegrationId);

        internal static bool IsDataCollectorDomain
        {
            get
            {
                if (_isDataCollectorDomainCache is { } value)
                {
                    return value;
                }

                // If the AppDomainName contains "DataCollector" we are in the data collector domain
#if NETCOREAPP
                return (_isDataCollectorDomainCache = DomainMetadata.Instance.AppDomainName.Contains("datacollector", StringComparison.OrdinalIgnoreCase)).Value;
#else
                return (_isDataCollectorDomainCache = DomainMetadata.Instance.AppDomainName.ToLowerInvariant().Contains("datacollector")).Value;
#endif
            }
        }

        internal static bool IsMsBuildTask
        {
            get
            {
                if (_isMsBuildTaskCache is { } value)
                {
                    return value;
                }

                // Let's try to detect if we are in the MSBuild task scenario
                // the process name is not guaranteed so we need to check the stack trace
                // to see if we are getting called from the Coverlet.MSbuild.Tasks namespace
                // because we don't need any symbols this should be fast enough.
                if (new StackTrace(3, false).GetFrames() is { } frames)
                {
                    foreach (var stackFrame in frames)
                    {
                        if (stackFrame is null)
                        {
                            continue;
                        }

                        if (stackFrame.GetMethod()?.DeclaringType?.FullName?.StartsWith("Coverlet.MSbuild.Tasks") == true)
                        {
                            _isMsBuildTaskCache = true;
                            return true;
                        }
                    }

                    _isMsBuildTaskCache = false;
                }

                return _isMsBuildTaskCache ?? false;
            }
        }

        internal static TestSession? CreateSession()
        {
            SetInjectedSessionCoverletXmlFallbackEnabled(false);

            // We create test session if not DataCollector
            if (IsDataCollectorDomain)
            {
                return null;
            }

            if (IsVSTestArtifactsPostprocessCommand(Environment.CommandLine))
            {
                Log.Debug("CreateSession: VSTest artifacts postprocess invocation detected. Session was not created.");
                return null;
            }

            // Let's detect if we already have a session for this test process
            var context = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(
                EnvironmentHelpers.GetEnvironmentVariables(),
                new DictionaryGetterAndSetter(DictionaryGetterAndSetter.EnvironmentVariableKeyProcessor));

            if (context.SpanContext is not null)
            {
                // Session found in the environment variables
                // let's capture current Coverlet XML attachments for stale-attachment filtering and bail out.
                CaptureCoverletCollectorXmlReportBaseline(GetInjectedSessionCoverageBackfillCommandLine(), GetInjectedSessionCoverageBackfillWorkingDirectory());
                SetInjectedSessionCoverletXmlFallbackEnabled(true);
                return null;
            }

            var testOptimization = TestOptimization.Instance;
            var testOptimizationSettings = testOptimization.Settings;
            var agentless = testOptimizationSettings.Agentless;
            var isEvpProxy = (testOptimization.TracerManagement?.EventPlatformProxySupport ?? EventPlatformProxySupport.None) != EventPlatformProxySupport.None;

            Log.Information("CreateSession: Agentless: {Agentless} | IsEvpProxy: {IsEvpProxy}", agentless, isEvpProxy);

            // We create a test session if the flag is turned on (agentless or evp proxy)
            if (agentless || isEvpProxy)
            {
                // Try to load the command line propagated from the dd-trace ci run command
                if (EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand) is not { Length: > 0 } commandLine)
                {
                    commandLine = Environment.CommandLine;
                }

                // Try to load the working directory propagated from the dd-trace ci run command
                if (EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory) is not { Length: > 0 } workingDirectory)
                {
                    workingDirectory = Environment.CurrentDirectory;
                }

                CaptureCoverletCollectorXmlReportBaseline(GetCoverageBackfillCommandLine(commandLine), workingDirectory);

                var session = TestSession.GetOrCreate(commandLine, workingDirectory, null, null, true);
                session.SetTag(IntelligentTestRunnerTags.TestTestsSkippingEnabled, testOptimization.SkippableFeature?.Enabled == true ? "true" : "false");
                session.SetTag(CodeCoverageTags.Enabled, testOptimizationSettings.CodeCoverageEnabled == true ? "true" : "false");
                if (testOptimization.EarlyFlakeDetectionFeature?.Enabled == true)
                {
                    session.SetTag(EarlyFlakeDetectionTags.Enabled, "true");
                }

                // At session level we know if the ITR is disabled (meaning that no tests will be skipped)
                // In that case we tell the backend no tests are going to be skipped.
                if (testOptimization.SkippableFeature?.Enabled == false)
                {
                    session.SetTag(IntelligentTestRunnerTags.TestsSkipped, "false");
                }

                // We enable the IPC server for the session
                session.EnableIpcServer();

                return session;
            }

            Log.Information("CreateSession: Session was not created.");
            return null;
        }

        /// <summary>
        /// Detects the VSTest artifacts postprocess invocation started after the real test run when artifact collection is enabled.
        /// </summary>
        /// <param name="commandLine">Current process command line.</param>
        /// <returns>True when the command line belongs to the VSTest artifacts postprocess phase.</returns>
        internal static bool IsVSTestArtifactsPostprocessCommand(string? commandLine)
            => commandLine?.IndexOf(VSTestArtifactsPostprocessArgument, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// Captures Coverlet collector XML attachments that already exist before the test command writes current results.
        /// </summary>
        /// <param name="commandLine">Test command line used to resolve VSTest result directories.</param>
        /// <param name="workingDirectory">Command working directory used for relative result directories.</param>
        private static void CaptureCoverletCollectorXmlReportBaseline(string? commandLine, string? workingDirectory)
        {
            try
            {
                var resultsDirectories = NormalizeCoverageReportPaths(GetCoverletCollectorResultsDirectories(commandLine, workingDirectory));
                var existingReports = GetAllCoverletCollectorXmlReportPaths(resultsDirectories);
                lock (CoverletCollectorXmlReportBaselineLock)
                {
                    _coverletCollectorXmlReportBaseline = new CoverletCollectorXmlReportBaseline(resultsDirectories, existingReports);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "RunCiCommand: Coverlet collector XML report baseline could not be captured.");
                lock (CoverletCollectorXmlReportBaselineLock)
                {
                    _coverletCollectorXmlReportBaseline = null;
                }
            }
        }

        internal static void FinalizeSession(TestSession? session, int exitCode, Exception? exception)
        {
            if (session is null)
            {
                if (TryConsumeInjectedSessionCoverletXmlFallbackEnabled())
                {
                    TryProcessInjectedSessionCoverletCollectorXmlReports(recordIpcFailureOnFailure: true);
                }

                return;
            }

            session.SetTag(TestTags.CommandExitCode, exitCode);

            if (exception is not null)
            {
                session.SetErrorInfo(exception);
            }

            var codeCoveragePath = EnvironmentHelpers.GetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath);

            // If the code coverage path is set we try to read all json files created, merge them into a single one and extract the
            // global code coverage percentage.
            // Note: we also write the total global code coverage to the `session-coverage-{date}.json` file
            if (!StringUtil.IsNullOrEmpty(codeCoveragePath))
            {
                try
                {
                    var outputPath = Path.Combine(codeCoveragePath, $"session-coverage-{DateTime.Now:yyyy-MM-dd_HH_mm_ss}.json");
                    if (CoverageUtils.TryCombineAndGetTotalCoverage(codeCoveragePath, outputPath, out var globalCoverage) &&
                        globalCoverage is not null)
                    {
                        var backfillResult = TryApplyItrCoverageBackfill(session, globalCoverage);
                        if (!backfillResult.CanPublishCoverage)
                        {
                            Log.Warning("RunCiCommand: ITR coverage backfill could not match backend coverage to Datadog internal coverage. The coverage result will not be published.");
                            TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageErrors();
                            TryDeleteFile(outputPath);
                        }
                        else
                        {
                            if (backfillResult.Backfilled)
                            {
                                File.WriteAllText(outputPath, JsonHelper.SerializeObject(globalCoverage));
                            }

                            // We only report the code coverage percentage if the customer manually sets the 'DD_CIVISIBILITY_CODE_COVERAGE_ENABLED' environment variable according to the new spec.
                            if (EnvironmentHelpers.GetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage)?.ToBoolean() == true)
                            {
                                var data = globalCoverage.Data;
                                session.RecordCodeCoverage(
                                    CodeCoverageReportSource.DatadogInternal,
                                    globalCoverage.GetTotalPercentage(),
                                    backfillResult.Backfilled,
                                    executableLines: data[1],
                                    coveredLines: data[2]);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "RunCiCommand: Error while reading or backfilling Datadog internal code coverage.");
                    TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageErrors();
                }
            }

            try
            {
                if (EnvironmentHelpers.GetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath) is { Length: > 0 } extCodeCoverageFilePath)
                {
                    if (!File.Exists(extCodeCoverageFilePath))
                    {
                        Log.Warning("RunCiCommand: The configured external code coverage file was not found: {Path}", extCodeCoverageFilePath);
                        TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageErrors();
                    }
                    else if (!CanProcessExternalCoveragePathForCurrentSession(extCodeCoverageFilePath, session.StartTime, out var reason, session))
                    {
                        Log.Warning("RunCiCommand: The configured external code coverage file will not be processed. {Reason}", reason);
                    }
                    else if (TryProcessCoverageXml(extCodeCoverageFilePath, session, out var coverageResult))
                    {
                        session.RecordCodeCoverage(
                            CodeCoverageReportSource.ExternalXml,
                            coverageResult.Percentage,
                            coverageResult.Backfilled,
                            coverageResult.ExecutableLines,
                            coverageResult.CoveredLines,
                            coverageResult.Diagnostic);
                    }
                    else
                    {
                        Log.Warning("RunCiCommand: Error while reading the external file code coverage. Format is not supported.");
                        TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageErrors();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "RunCiCommand: Error while reading the external file code coverage.");
            }

            FinalizeCoverageResultsBeforeSessionClose(session);

            session.Close(exitCode == 0 ? TestStatus.Pass : TestStatus.Fail);
        }

        internal static void FinalizeCoverageResultsBeforeSessionClose(TestSession session)
            => FinalizeCoverageResultsBeforeSessionClose(session, static session => TryProcessCoverletCollectorXmlReports(session, recordCoverageResult: true));

        /// <summary>
        /// Checks whether a configured external coverage path can be processed for the current test session.
        /// </summary>
        /// <param name="externalCoveragePath">Configured external coverage report path.</param>
        /// <param name="sessionStartTime">Current test session start time.</param>
        /// <param name="reason">Reason why the path should not be processed.</param>
        /// <param name="session">Current test session, when the check should use session-scoped ITR skip state.</param>
        /// <returns>True when the path is safe to process for this session.</returns>
        internal static bool CanProcessExternalCoveragePathForCurrentSession(string externalCoveragePath, DateTimeOffset sessionStartTime, out string reason, TestSession? session = null)
        {
            if ((CoverageBackfillCapability.IsExternalCoveragePathWrittenByCurrentCommand(externalCoveragePath) || HasActualItrSkips(session)) &&
                !IsCoverageReportWrittenDuringSession(externalCoveragePath, sessionStartTime))
            {
                reason = "External coverage XML report was not written during the current test session.";
                return false;
            }

            return CoverageBackfillCapability.CanProcessExternalCoveragePathForCurrentCommand(externalCoveragePath, out reason);
        }

        internal static void FinalizeCoverageResultsBeforeSessionClose(TestSession session, ProcessCoverletCollectorXmlReports processCoverletCollectorXmlReports)
        {
            var coverletXmlProcessingResult = CoverletCollectorXmlProcessingResult.NotApplicable;
            var hasCoverageIpcFailure = false;
            string? ipcFailure = null;
            try
            {
                session.DrainIpcMessages(
                    TimeSpan.FromMilliseconds(250),
                    TimeSpan.FromMilliseconds(50),
                    waitForFirstMessage: CoverageBackfillCapability.ShouldWaitForCoverageIpc(TestOptimization.Instance.Settings));
                session.WaitForActiveIpcCallbacks(TimeSpan.FromMilliseconds(100));
                session.RecordPersistedCoverageIpcResults();
                coverletXmlProcessingResult = processCoverletCollectorXmlReports(session);

                // A final quiet-period drain lets late IPC messages that arrived while XML fallback was being checked
                // participate in the coverage result arbitration before coverage tags are published.
                session.DrainIpcMessages(
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromMilliseconds(25),
                    waitForFirstMessage: false);
                session.WaitForActiveIpcCallbacks(TimeSpan.FromMilliseconds(100));
                session.RecordPersistedCoverageIpcResults();
                hasCoverageIpcFailure = CoverageBackfillDataStore.TryReadCoverageIpcFailure(session.Tags.SessionId, out ipcFailure);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "RunCiCommand: Error while draining coverage IPC or processing Coverlet collector XML fallback.");
                TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageErrors();
                coverletXmlProcessingResult = CoverletCollectorXmlProcessingResult.FailedClosed;
            }
            finally
            {
                SuppressUnvalidatedExternalCoverageResultsAfterActualItrSkip(session, coverletXmlProcessingResult);
            }

            if (hasCoverageIpcFailure &&
                coverletXmlProcessingResult != CoverletCollectorXmlProcessingResult.Processed &&
                !session.HasPublishableCodeCoverageResult)
            {
                Log.Warning("RunCiCommand: A selected coverage tool could not deliver its coverage result through IPC: {Reason}", ipcFailure);
                TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageErrors();
            }
        }

        /// <summary>
        /// Merges backend skipped-test coverage into Datadog internal global coverage when the session actually skipped tests by ITR.
        /// </summary>
        /// <param name="session">The test session that will publish the coverage result.</param>
        /// <param name="globalCoverage">The local global coverage model collected from executed tests.</param>
        /// <returns>Result of the backfill merge operation.</returns>
        internal static CoverageBackfillResult TryApplyItrCoverageBackfill(TestSession session, GlobalCoverageInfo globalCoverage)
        {
            var availability = GetCoverageBackfillDataForSession(session, out var backfillData);
            if (availability == CoverageBackfillAvailability.NotRequired)
            {
                return new CoverageBackfillResult(coverageDataEvaluated: false, matchedFiles: 0, updatedFiles: 0);
            }

            if (availability == CoverageBackfillAvailability.Unavailable)
            {
                return new CoverageBackfillResult(coverageDataEvaluated: false, matchedFiles: 0, updatedFiles: 0, canPublishCoverage: false);
            }

            var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfillData, CIEnvironmentValues.Instance);
            if (result.Applied)
            {
                Log.Information<int, int>(
                    "RunCiCommand: ITR coverage backfill applied to internal coverage. MatchedFiles={MatchedFiles}, UpdatedFiles={UpdatedFiles}",
                    result.MatchedFiles,
                    result.UpdatedFiles);
            }

            return result;
        }

        /// <summary>
        /// Best-effort deletion for generated coverage output that should not be published.
        /// </summary>
        /// <param name="filePath">Generated coverage file path.</param>
        private static void TryDeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "RunCiCommand: Error deleting temporary coverage file.");
            }
        }

        private static void SuppressUnvalidatedExternalCoverageResultsAfterActualItrSkip(TestSession session, CoverletCollectorXmlProcessingResult coverletXmlProcessingResult)
        {
            if (!HasActualItrSkips(session))
            {
                return;
            }

            if (coverletXmlProcessingResult is CoverletCollectorXmlProcessingResult.FailedClosed or CoverletCollectorXmlProcessingResult.NotApplicable)
            {
                session.SuppressUnvalidatedCodeCoverageResult(CodeCoverageReportSource.MicrosoftCodeCoverage);
                session.SuppressUnvalidatedCodeCoverageResult(CodeCoverageReportSource.Coverlet);
                session.SuppressUnvalidatedCodeCoverageResult(CodeCoverageReportSource.CoverletXmlFallback);
                return;
            }

            session.SuppressUnvalidatedBackfilledCodeCoverageResult(CodeCoverageReportSource.MicrosoftCodeCoverage);

            if (coverletXmlProcessingResult == CoverletCollectorXmlProcessingResult.Processed)
            {
                return;
            }

            session.SuppressUnvalidatedBackfilledCodeCoverageResult(CodeCoverageReportSource.Coverlet);
            session.SuppressUnvalidatedBackfilledCodeCoverageResult(CodeCoverageReportSource.CoverletXmlFallback);
        }

        /// <summary>
        /// Reads and optionally backfills an external XML coverage report for the supplied session.
        /// </summary>
        /// <param name="filePath">Coverage XML report path.</param>
        /// <param name="session">Test session that may publish the result.</param>
        /// <param name="result">Coverage result after optional backfill.</param>
        /// <returns>True if the XML report was parsed successfully.</returns>
        internal static bool TryProcessCoverageXml(string filePath, TestSession? session, out ExternalCoverageXmlResult result)
        {
            CoverageBackfillData? backfillData = null;
            if (session is null)
            {
                return ExternalCoverageXmlBackfill.TryProcess(filePath, backfillData, applyBackfill: false, out result);
            }

            var availability = GetCoverageBackfillDataForSession(session, out backfillData);
            if (availability == CoverageBackfillAvailability.Unavailable)
            {
                result = default;
                return false;
            }

            return ExternalCoverageXmlBackfill.TryProcess(filePath, backfillData, availability == CoverageBackfillAvailability.Available, out result);
        }

        private static bool TryProcessCoverageXml(string filePath, CoverletCollectorXmlReportFormat format, TestSession? session, ExternalCoverageXmlBackfill.CoverageBackfillValidationState? validationState, out ExternalCoverageXmlResult result)
        {
            CoverageBackfillData? backfillData = null;
            if (session is null)
            {
                return TryProcessCoverletCollectorXml(filePath, format, backfillData, applyBackfill: false, validationState, out result);
            }

            var availability = GetCoverageBackfillDataForSession(session, out backfillData);
            if (availability == CoverageBackfillAvailability.Unavailable)
            {
                result = default;
                return false;
            }

            return TryProcessCoverletCollectorXml(filePath, format, backfillData, availability == CoverageBackfillAvailability.Available, validationState, out result);
        }

        private static bool TryProcessCoverletCollectorXml(string filePath, CoverletCollectorXmlReportFormat format, CoverageBackfillData? backfillData, bool applyBackfill, ExternalCoverageXmlBackfill.CoverageBackfillValidationState? validationState, out ExternalCoverageXmlResult result)
        {
            return format switch
            {
                CoverletCollectorXmlReportFormat.Cobertura => ExternalCoverageXmlBackfill.TryProcessCobertura(filePath, backfillData, applyBackfill, validationState, out result),
                CoverletCollectorXmlReportFormat.OpenCover => ExternalCoverageXmlBackfill.TryProcessOpenCover(filePath, backfillData, applyBackfill, validationState, out result),
                _ => ExternalCoverageXmlBackfill.TryProcess(filePath, backfillData, applyBackfill, validationState, out result),
            };
        }

        /// <summary>
        /// Locates the VSTest results directory that can contain Coverlet collector Cobertura attachments.
        /// </summary>
        /// <param name="commandLine">Test command line used to detect Coverlet and parse result-directory switches.</param>
        /// <param name="workingDirectory">Command working directory used for relative result directories and VSTest defaults.</param>
        /// <param name="resultsDirectory">Resolved absolute results directory.</param>
        /// <returns>True when the command uses the Coverlet collector and a results directory can be resolved.</returns>
        internal static bool TryGetCoverletCollectorResultsDirectory(string? commandLine, string? workingDirectory, out string resultsDirectory)
        {
            resultsDirectory = string.Empty;
            var resultsDirectories = GetCoverletCollectorResultsDirectories(commandLine, workingDirectory);
            if (resultsDirectories.Length > 0)
            {
                resultsDirectory = resultsDirectories[0];
                return true;
            }

            return false;
        }

        /// <summary>
        /// Locates every VSTest results directory candidate that can contain Coverlet collector Cobertura attachments.
        /// </summary>
        /// <param name="commandLine">Test command line used to detect Coverlet and parse result-directory switches.</param>
        /// <param name="workingDirectory">Command working directory used for relative result directories and VSTest defaults.</param>
        /// <returns>Resolved absolute results directory candidates, ordered from most specific to least specific.</returns>
        internal static string[] GetCoverletCollectorResultsDirectories(string? commandLine, string? workingDirectory)
        {
            var absoluteWorkingDirectory = GetAbsoluteWorkingDirectory(workingDirectory);
            var command = CoverageBackfillCommandLine.Parse(commandLine, absoluteWorkingDirectory);
            var resultsDirectories = new List<string>();

            foreach (var baseDirectory in GetCoverletCollectorBaseDirectories(workingDirectory))
            {
                AddCoverletCollectorResultsDirectories(resultsDirectories, command, baseDirectory);
            }

            return resultsDirectories.ToArray();
        }

        /// <summary>
        /// Rewrites Coverlet collector XML attachments after VSTest exits so report files stay accurate even when IPC is unavailable.
        /// </summary>
        /// <param name="session">Current test session.</param>
        /// <param name="recordCoverageResult">Whether processed XML results should be recorded for session coverage publication.</param>
        /// <returns>True when at least one Coverlet XML report was processed.</returns>
        private static CoverletCollectorXmlProcessingResult TryProcessCoverletCollectorXmlReports(TestSession session, bool recordCoverageResult)
        {
            return TryProcessCoverletCollectorXmlReports(
                GetCoverageBackfillCommandLine(session.Command),
                session.WorkingDirectory,
                session.StartTime,
                (CoverletCollectorXmlReport coverageReport, ExternalCoverageXmlBackfill.CoverageBackfillValidationState validationState, out ExternalCoverageXmlResult result) => TryProcessCoverageXml(coverageReport.FilePath, coverageReport.Format, session, validationState, out result),
                (coverageReports, coverageResult, backfillValidated) =>
                {
                    if (recordCoverageResult)
                    {
                        var resultId = GetCoverletXmlFallbackResultId(session.Tags.SessionId, coverageReports);
                        if (coverageReports.Count > 1)
                        {
                            var supersededResultIds = coverageReports.Select(coverageReport => GetCoverletXmlFallbackResultId(session.Tags.SessionId, coverageReport)).ToArray();
                            session.RecordMergedCodeCoverage(
                                CodeCoverageReportSource.CoverletXmlFallback,
                                coverageResult.Percentage,
                                coverageResult.Backfilled,
                                coverageResult.ExecutableLines,
                                coverageResult.CoveredLines,
                                coverageResult.Diagnostic,
                                resultId,
                                supersededResultIds,
                                backfillValidated);
                        }
                        else
                        {
                            session.RecordCodeCoverage(
                                CodeCoverageReportSource.CoverletXmlFallback,
                                coverageResult.Percentage,
                                coverageResult.Backfilled,
                                coverageResult.ExecutableLines,
                                coverageResult.CoveredLines,
                                coverageResult.Diagnostic,
                                resultId,
                                backfillValidated);
                        }
                    }
                });
        }

        /// <summary>
        /// Rewrites and reports Coverlet collector XML attachments when this process participates in an externally propagated session.
        /// </summary>
        /// <returns>True when at least one Coverlet XML report was processed.</returns>
        internal static bool TryProcessInjectedSessionCoverletCollectorXmlReports()
            => TryProcessInjectedSessionCoverletCollectorXmlReports(recordIpcFailureOnFailure: false) == CoverletCollectorXmlProcessingResult.Processed;

        private static CoverletCollectorXmlProcessingResult TryProcessInjectedSessionCoverletCollectorXmlReports(bool recordIpcFailureOnFailure)
        {
            var context = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(
                EnvironmentHelpers.GetEnvironmentVariables(),
                new DictionaryGetterAndSetter(DictionaryGetterAndSetter.EnvironmentVariableKeyProcessor));

            if (context.SpanContext is not { } sessionContext)
            {
                return CoverletCollectorXmlProcessingResult.NotApplicable;
            }

            try
            {
                var commandLine = GetInjectedSessionCoverageBackfillCommandLine();
                var workingDirectory = GetInjectedSessionCoverageBackfillWorkingDirectory();
                if (!IsCoverletCollectorXmlFallbackCommand(commandLine, workingDirectory))
                {
                    return CoverletCollectorXmlProcessingResult.NotApplicable;
                }

                var result = TryProcessCoverletCollectorXmlReports(
                    commandLine,
                    workingDirectory,
                    GetCurrentProcessStartTime(),
                    (CoverletCollectorXmlReport coverageReport, ExternalCoverageXmlBackfill.CoverageBackfillValidationState validationState, out ExternalCoverageXmlResult result) => TryProcessCoverletCollectorXmlForCurrentProcess(sessionContext.SpanId, coverageReport, validationState, out result),
                    (coverageReports, coverageResult, backfillValidated) => SendInjectedSessionCoverageResult(sessionContext.SpanId, coverageReports, coverageResult, backfillValidated));
                if (result != CoverletCollectorXmlProcessingResult.Processed && recordIpcFailureOnFailure)
                {
                    RecordCoverletXmlFallbackIpcFailure(sessionContext.SpanId);
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "RunCiCommand: Error while processing injected-session Coverlet collector XML fallback.");
                if (recordIpcFailureOnFailure)
                {
                    RecordCoverletXmlFallbackIpcFailure(sessionContext.SpanId);
                }

                return CoverletCollectorXmlProcessingResult.FailedClosed;
            }
        }

        /// <summary>
        /// Rewrites Coverlet collector XML attachments after VSTest exits so report files stay accurate even when IPC is unavailable.
        /// </summary>
        /// <param name="commandLine">Test command line used to detect Coverlet and parse result-directory switches.</param>
        /// <param name="workingDirectory">Command working directory used for relative result directories and VSTest defaults.</param>
        /// <param name="sessionStartTime">Current test process or session start time.</param>
        /// <param name="processCoverageXml">Coverage XML processor for the current session model.</param>
        /// <param name="recordCoverageResult">Callback that records processed coverage results.</param>
        /// <returns>Processing status for the selected Coverlet XML report set.</returns>
        private static CoverletCollectorXmlProcessingResult TryProcessCoverletCollectorXmlReports(
            string? commandLine,
            string? workingDirectory,
            DateTimeOffset sessionStartTime,
            TryProcessCoverageXmlReport processCoverageXml,
            RecordCoverageXmlResult recordCoverageResult)
        {
            var resultsDirectories = GetCoverletCollectorResultsDirectories(commandLine, workingDirectory);
            if (resultsDirectories.Length == 0)
            {
                return CoverletCollectorXmlProcessingResult.NotApplicable;
            }

            var coverageReports = GetCoverletCollectorXmlReports(resultsDirectories, sessionStartTime);
            if (coverageReports.Length == 0)
            {
                return CoverletCollectorXmlProcessingResult.NotApplicable;
            }

            var selectedCoverageReportGroups = SelectCoverletCollectorXmlReportGroupsForProcessing(coverageReports);
            var coverageResultCandidateGroups = new List<List<ProcessedCoverletCollectorXmlReport>>(selectedCoverageReportGroups.Count);
            var coverageReportBackups = new List<CoverageXmlReportBackup>(selectedCoverageReportGroups.Count);
            var selectedCoverageReportPaths = new HashSet<string>(CoverageReportPathComparer);
            foreach (var selectedCoverageReportGroup in selectedCoverageReportGroups)
            {
                if (!TryProcessSelectedCoverletCollectorXmlReportCandidates(
                        selectedCoverageReportGroup,
                        processCoverageXml,
                        coverageReportBackups,
                        out var coverageResultCandidates))
                {
                    TryRestoreCoverageXmlReports(coverageReportBackups);
                    Log.Warning("RunCiCommand: No Coverlet collector XML report in an attachment directory could be processed safely.");
                    TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageErrors();
                    return CoverletCollectorXmlProcessingResult.FailedClosed;
                }

                coverageResultCandidateGroups.Add(coverageResultCandidates);
            }

            if (!TrySelectCoverletCollectorXmlReportSet(coverageResultCandidateGroups, out var coverageResults, out var selectedValidationState))
            {
                TryRestoreCoverageXmlReports(coverageReportBackups);
                Log.Warning("RunCiCommand: Selected Coverlet collector XML reports could not be safely reconciled with backend ITR coverage, so no stale coverage percentage will be sent.");
                TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageErrors();
                return CoverletCollectorXmlProcessingResult.FailedClosed;
            }

            foreach (var processedReport in coverageResults)
            {
                selectedCoverageReportPaths.Add(processedReport.Report.FilePath);
            }

            TryRestoreUnselectedProcessedCoverletCollectorXmlReports(coverageResultCandidateGroups, selectedCoverageReportPaths);
            var orderedCoverageResults = coverageResults.OrderBy(report => report.Order).ToArray();
            if (!TryMergeCoverageXmlResults(orderedCoverageResults, selectedValidationState, out var mergedCoverageResult))
            {
                TryRestoreCoverageXmlReports(coverageReportBackups);
                Log.Warning("RunCiCommand: Selected Coverlet collector XML reports could not be aggregated, so no stale coverage percentage will be sent.");
                TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageErrors();
                return CoverletCollectorXmlProcessingResult.FailedClosed;
            }

            recordCoverageResult(orderedCoverageResults.Select(report => report.Report).ToArray(), mergedCoverageResult, selectedValidationState.BackfillValidated);
            TryProcessUnselectedCoverletCollectorXmlReports(coverageReports, selectedCoverageReportPaths, processCoverageXml);

            if (coverageResults.Count > 0)
            {
                Log.Information<int>("RunCiCommand: Coverlet collector XML reports processed. Count={Count}", coverageResults.Count);
            }

            return coverageResults.Count > 0 ?
                       CoverletCollectorXmlProcessingResult.Processed :
                       CoverletCollectorXmlProcessingResult.NotApplicable;
        }

        private static void TryProcessUnselectedCoverletCollectorXmlReports(
            CoverletCollectorXmlReport[] coverageReports,
            HashSet<string> selectedCoverageReportPaths,
            TryProcessCoverageXmlReport processCoverageXml)
        {
            var unselectedReportGroups = coverageReports
                                        .Where(coverageReport => !selectedCoverageReportPaths.Contains(coverageReport.FilePath))
                                        .GroupBy(coverageReport => coverageReport.PublishPriority)
                                        .OrderBy(group => group.Key);

            foreach (var unselectedReportGroup in unselectedReportGroups)
            {
                TryProcessUnselectedCoverletCollectorXmlReportGroup(unselectedReportGroup.ToArray(), processCoverageXml);
            }
        }

        private static void TryProcessUnselectedCoverletCollectorXmlReportGroup(
            CoverletCollectorXmlReport[] coverageReports,
            TryProcessCoverageXmlReport processCoverageXml)
        {
            var backups = new List<CoverageXmlReportBackup>(coverageReports.Length);
            try
            {
                var validationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();
                foreach (var coverageReport in coverageReports)
                {
                    if (!TryReadCoverageXmlReportBackup(coverageReport.FilePath, out var backup))
                    {
                        TryRestoreCoverageXmlReports(backups);
                        return;
                    }

                    backups.Add(backup);
                    var reportValidationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState(rejectDuplicateRepresentedBackendLines: true);
                    if (!processCoverageXml(coverageReport, reportValidationState, out _))
                    {
                        TryRestoreCoverageXmlReports(backups);
                        Log.Debug("RunCiCommand: Unselected Coverlet collector XML report set could not be processed safely, so it was restored.");
                        return;
                    }

                    if (reportValidationState.HasDuplicateRepresentedBackendLine)
                    {
                        TryRestoreCoverageXmlReports(backups);
                        Log.Debug("RunCiCommand: Unselected Coverlet collector XML report contained duplicate backend lines, so it was restored.");
                        return;
                    }

                    validationState.Merge(reportValidationState);
                }

                if (!validationState.CanKeepUnpublishedBackfill())
                {
                    TryRestoreCoverageXmlReports(backups);
                    Log.Debug("RunCiCommand: Unselected Coverlet collector XML report set could not be safely backfilled, so it was restored.");
                }
            }
            catch (Exception ex)
            {
                TryRestoreCoverageXmlReports(backups);
                Log.Debug(ex, "RunCiCommand: Error while processing unselected Coverlet collector XML report set.");
            }
        }

        private static bool TryProcessSelectedCoverletCollectorXmlReportCandidates(
            List<SelectedCoverletCollectorXmlReport> selectedCoverageReports,
            TryProcessCoverageXmlReport processCoverageXml,
            List<CoverageXmlReportBackup> coverageReportBackups,
            out List<ProcessedCoverletCollectorXmlReport> processedReports)
        {
            processedReports = new List<ProcessedCoverletCollectorXmlReport>(selectedCoverageReports.Count);
            foreach (var selectedCoverageReport in selectedCoverageReports)
            {
                var coverageReport = selectedCoverageReport.Report;
                if (!TryReadCoverageXmlReportBackup(coverageReport.FilePath, out var backup))
                {
                    continue;
                }

                coverageReportBackups.Add(backup);
                var validationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();
                if (processCoverageXml(coverageReport, validationState, out var coverageResult))
                {
                    processedReports.Add(new ProcessedCoverletCollectorXmlReport(coverageReport, coverageResult, validationState, selectedCoverageReport.Order, backup));
                    continue;
                }

                TryRestoreCoverageXmlReport(backup);
                Log.Debug("RunCiCommand: Coverlet collector XML report could not be processed safely, trying another report in the same attachment directory: {Path}", coverageReport.FilePath);
            }

            return processedReports.Count > 0;
        }

        private static bool TrySelectCoverletCollectorXmlReportSet(
            List<List<ProcessedCoverletCollectorXmlReport>> candidateGroups,
            out List<ProcessedCoverletCollectorXmlReport> selectedReports,
            out ExternalCoverageXmlBackfill.CoverageBackfillValidationState selectedValidationState)
        {
            selectedReports = new List<ProcessedCoverletCollectorXmlReport>(candidateGroups.Count);
            selectedValidationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();
            if (candidateGroups.Count == 0)
            {
                return false;
            }

            var currentReports = new List<ProcessedCoverletCollectorXmlReport>(candidateGroups.Count);
            List<ProcessedCoverletCollectorXmlReport>? selectedCandidateReports = null;
            ExternalCoverageXmlBackfill.CoverageBackfillValidationState? selectedCandidateValidationState = null;
            if (!TrySelectCandidate(groupIndex: 0))
            {
                return false;
            }

            selectedReports = selectedCandidateReports!;
            selectedValidationState = selectedCandidateValidationState!;
            return true;

            bool TrySelectCandidate(int groupIndex)
            {
                if (groupIndex == candidateGroups.Count)
                {
                    var validationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();
                    foreach (var processedReport in currentReports)
                    {
                        validationState.Merge(processedReport.ValidationState);
                    }

                    if (!validationState.CanPublish())
                    {
                        return false;
                    }

                    if (!TryMergeCoverageXmlResults(currentReports, validationState, out _))
                    {
                        return false;
                    }

                    if (selectedCandidateReports is null ||
                        IsBetterCoverletXmlReportSet(currentReports, validationState, selectedCandidateReports, selectedCandidateValidationState!))
                    {
                        selectedCandidateReports = currentReports.ToList();
                        selectedCandidateValidationState = validationState;
                    }

                    return true;
                }

                var foundCandidate = false;
                foreach (var candidate in candidateGroups[groupIndex])
                {
                    currentReports.Add(candidate);
                    if (TrySelectCandidate(groupIndex + 1))
                    {
                        foundCandidate = true;
                    }

                    currentReports.RemoveAt(currentReports.Count - 1);
                }

                return foundCandidate;
            }
        }

        private static bool IsBetterCoverletXmlReportSet(
            IReadOnlyList<ProcessedCoverletCollectorXmlReport> candidateReports,
            ExternalCoverageXmlBackfill.CoverageBackfillValidationState candidateValidationState,
            IReadOnlyList<ProcessedCoverletCollectorXmlReport> selectedReports,
            ExternalCoverageXmlBackfill.CoverageBackfillValidationState selectedValidationState)
        {
            var representedBackendLineComparison = candidateValidationState.RepresentedBackendLineCount.CompareTo(selectedValidationState.RepresentedBackendLineCount);
            if (representedBackendLineComparison != 0)
            {
                return representedBackendLineComparison > 0;
            }

            var candidateOrder = SumCoverletXmlReportOrder(candidateReports);
            var selectedOrder = SumCoverletXmlReportOrder(selectedReports);
            return candidateOrder < selectedOrder;
        }

        private static int SumCoverletXmlReportOrder(IReadOnlyList<ProcessedCoverletCollectorXmlReport> reports)
        {
            var order = 0;
            for (var i = 0; i < reports.Count; i++)
            {
                order += reports[i].Order;
            }

            return order;
        }

        private static void TryRestoreUnselectedProcessedCoverletCollectorXmlReports(
            List<List<ProcessedCoverletCollectorXmlReport>> candidateGroups,
            HashSet<string> selectedCoverageReportPaths)
        {
            foreach (var candidateGroup in candidateGroups)
            {
                foreach (var candidate in candidateGroup)
                {
                    if (!selectedCoverageReportPaths.Contains(candidate.Report.FilePath))
                    {
                        TryRestoreCoverageXmlReport(candidate.Backup);
                    }
                }
            }
        }

        /// <summary>
        /// Reads and optionally backfills an XML coverage report for the current propagated session process.
        /// </summary>
        /// <param name="sessionId">Propagated parent test session span id, or 0 for legacy unscoped state.</param>
        /// <param name="coverageReport">Discovered Coverlet collector XML report.</param>
        /// <param name="validationState">Report-set validation data to update while processing the XML report.</param>
        /// <param name="result">Coverage result after optional backfill.</param>
        /// <returns>True if the XML report was parsed successfully.</returns>
        private static bool TryProcessCoverletCollectorXmlForCurrentProcess(ulong sessionId, CoverletCollectorXmlReport coverageReport, ExternalCoverageXmlBackfill.CoverageBackfillValidationState validationState, out ExternalCoverageXmlResult result)
        {
            var availability = GetCoverageBackfillDataForCurrentProcess(sessionId, out var backfillData);
            if (availability == CoverageBackfillAvailability.Unavailable)
            {
                result = default;
                return false;
            }

            return TryProcessCoverletCollectorXml(coverageReport.FilePath, coverageReport.Format, backfillData, availability == CoverageBackfillAvailability.Available, validationState, out result);
        }

        /// <summary>
        /// Sends a Coverlet XML fallback result to an externally propagated session.
        /// </summary>
        /// <param name="sessionSpanId">Propagated parent test session span id.</param>
        /// <param name="coverageReports">Selected Coverlet XML attachments that produced the aggregated result.</param>
        /// <param name="coverageResult">Processed XML coverage result.</param>
        /// <param name="backfillValidated">Whether backend ITR coverage was reconciled without unsafe XML report matches.</param>
        private static void SendInjectedSessionCoverageResult(ulong sessionSpanId, IReadOnlyList<CoverletCollectorXmlReport> coverageReports, ExternalCoverageXmlResult coverageResult, bool backfillValidated)
        {
            Log.Information("CoverletXmlFallback.Percentage: {Value}", coverageResult.Percentage);
            var stableCoverageResultId = GetCoverletXmlFallbackResultId(sessionSpanId, coverageReports);
            var supersededResultIds = coverageReports.Count > 1 ?
                                          coverageReports.Select(coverageReport => GetCoverletXmlFallbackResultId(sessionSpanId, coverageReport)).ToArray() :
                                          null;
            _ = CoverageBackfillDataStore.RecordCoverageIpcResult(
                TestOptimization.Instance,
                sessionSpanId,
                CodeCoverageReportSource.CoverletXmlFallback,
                coverageResult.Percentage,
                coverageResult.Backfilled,
                coverageResult.ExecutableLines,
                coverageResult.CoveredLines,
                coverageResult.Diagnostic,
                stableCoverageResultId,
                backfillValidated: backfillValidated,
                supersededResultIds: supersededResultIds);

            try
            {
                var name = $"session_{sessionSpanId}";
                Log.Debug("CoverletXmlFallback.Enabling IPC client: {Name}", name);
                using var ipcClient = new IpcClient(name);
                Log.Debug("CoverletXmlFallback.Sending session code coverage: {Value}", coverageResult.Percentage);
                if (!ipcClient.TrySendMessage(
                        new SessionCodeCoverageMessage(
                            CodeCoverageReportSource.CoverletXmlFallback,
                            coverageResult.Percentage,
                            coverageResult.Backfilled,
                            coverageResult.ExecutableLines,
                            coverageResult.CoveredLines,
                            coverageResult.Diagnostic,
                            stableCoverageResultId,
                            backfillValidated,
                            supersededResultIds: supersededResultIds)))
                {
                    Log.Warning("RunCiCommand: Could not send Coverlet XML fallback code coverage IPC message.");
                    RecordCoverletXmlFallbackIpcFailure(sessionSpanId);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RunCiCommand: Error enabling IPC client and sending Coverlet XML fallback coverage data.");
                RecordCoverletXmlFallbackIpcFailure(sessionSpanId);
            }
        }

        private static void RecordCoverletXmlFallbackIpcFailure(ulong sessionSpanId)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageErrors();
            CoverageBackfillDataStore.RecordCoverageIpcFailure(sessionSpanId, nameof(CodeCoverageReportSource.CoverletXmlFallback));
        }

        internal static bool TryMergeCoverageXmlResults(IReadOnlyList<ExternalCoverageXmlResult> coverageResults, out ExternalCoverageXmlResult mergedCoverageResult)
            => TryMergeCoverageXmlResults(coverageResults, reportPaths: null, out mergedCoverageResult);

        internal static bool TryMergeCoverageXmlResults(IReadOnlyList<ExternalCoverageXmlResult> coverageResults, IReadOnlyList<string>? reportPaths, out ExternalCoverageXmlResult mergedCoverageResult)
            => TryMergeCoverageXmlResults(coverageResults, reportPaths, validationState: null, out mergedCoverageResult);

        internal static bool TryMergeCoverageXmlResults(
            IReadOnlyList<ExternalCoverageXmlResult> coverageResults,
            IReadOnlyList<string>? reportPaths,
            ExternalCoverageXmlBackfill.CoverageBackfillValidationState? validationState,
            out ExternalCoverageXmlResult mergedCoverageResult)
        {
            mergedCoverageResult = default;
            if (coverageResults.Count == 0)
            {
                return false;
            }

            if (coverageResults.Count == 1)
            {
                mergedCoverageResult = coverageResults[0];
                return true;
            }

            double executableLines = 0;
            double coveredLines = 0;
            var backfilled = false;
            var rewritten = false;
            string? diagnostic = null;
            foreach (var coverageResult in coverageResults)
            {
                if (coverageResult.ExecutableLines is not { } resultExecutableLines ||
                    coverageResult.CoveredLines is not { } resultCoveredLines)
                {
                    return false;
                }

                executableLines += resultExecutableLines;
                coveredLines += resultCoveredLines;
                backfilled |= coverageResult.Backfilled;
                rewritten |= coverageResult.Rewritten;
                diagnostic = diagnostic is null || diagnostic == coverageResult.Diagnostic ? coverageResult.Diagnostic : "multi-report";
            }

            if (reportPaths is not null)
            {
                if (reportPaths.Count != coverageResults.Count ||
                    !ExternalCoverageXmlBackfill.TryReadMergedLineCoverage(reportPaths, validationState, out var mergedExecutableLines, out var mergedCoveredLines))
                {
                    return false;
                }

                executableLines = mergedExecutableLines;
                coveredLines = mergedCoveredLines;
            }

            if (executableLines <= 0)
            {
                return false;
            }

            mergedCoverageResult = new ExternalCoverageXmlResult(
                ((coveredLines / executableLines) * 100).ToValidPercentage(),
                executableLines,
                coveredLines,
                backfilled,
                rewritten,
                diagnostic);
            return true;
        }

        private static bool TryMergeCoverageXmlResults(
            IReadOnlyList<ProcessedCoverletCollectorXmlReport> processedReports,
            ExternalCoverageXmlBackfill.CoverageBackfillValidationState selectedValidationState,
            out ExternalCoverageXmlResult mergedCoverageResult)
        {
            mergedCoverageResult = default;
            if (processedReports.Count == 0)
            {
                return false;
            }

            if (processedReports.Count == 1)
            {
                mergedCoverageResult = processedReports[0].Result;
                return true;
            }

            double executableLines = 0;
            double coveredLines = 0;
            var backfilled = false;
            var rewritten = false;
            string? diagnostic = null;
            var reportPaths = new List<string>(processedReports.Count);
            foreach (var processedReport in processedReports)
            {
                reportPaths.Add(processedReport.Report.FilePath);
                backfilled |= processedReport.Result.Backfilled;
                rewritten |= processedReport.Result.Rewritten;
                diagnostic = diagnostic is null || diagnostic == processedReport.Result.Diagnostic ? processedReport.Result.Diagnostic : "multi-report";
            }

            if (!ExternalCoverageXmlBackfill.TryReadMergedLineCoverage(reportPaths, selectedValidationState, out executableLines, out coveredLines))
            {
                return false;
            }

            if (executableLines <= 0)
            {
                return false;
            }

            mergedCoverageResult = new ExternalCoverageXmlResult(
                ((coveredLines / executableLines) * 100).ToValidPercentage(),
                executableLines,
                coveredLines,
                backfilled,
                rewritten,
                diagnostic);
            return true;
        }

        private static string GetCoverletXmlFallbackResultId(ulong sessionSpanId, IReadOnlyList<CoverletCollectorXmlReport> coverageReports)
        {
            if (coverageReports.Count == 1)
            {
                return GetCoverletXmlFallbackResultId(sessionSpanId, coverageReports[0]);
            }

            var orderedResultIds = coverageReports
                                  .Select(coverageReport => GetCoverletXmlFallbackResultId(sessionSpanId, coverageReport))
                                  .OrderBy(resultId => resultId, StringComparer.Ordinal);
            var payload = Encoding.UTF8.GetBytes($"{CodeCoverageReportSource.CoverletXmlFallback}|group|v1|{sessionSpanId}|{string.Join("|", orderedResultIds)}");
#if NET6_0_OR_GREATER
            var hash = SHA256.HashData(payload);
#else
            using System.Security.Cryptography.HashAlgorithm sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(payload);
#endif
            return $"coverlet-xml-{HexString.ToHexString(hash)}";
        }

        private static string GetCoverletXmlFallbackResultId(ulong sessionSpanId, CoverletCollectorXmlReport coverageReport)
        {
            var attachmentDirectory = GetAttachmentDirectoryKey(coverageReport.FilePath).Replace('\\', '/');
            var payload = Encoding.UTF8.GetBytes($"{CodeCoverageReportSource.CoverletXmlFallback}|v1|{sessionSpanId}|{attachmentDirectory}");
#if NET6_0_OR_GREATER
            var hash = SHA256.HashData(payload);
#else
            using System.Security.Cryptography.HashAlgorithm sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(payload);
#endif
            return $"coverlet-xml-{HexString.ToHexString(hash)}";
        }

        /// <summary>
        /// Captures the original bytes of a coverage XML report before any fallback rewrite is attempted.
        /// </summary>
        /// <param name="filePath">Coverage XML report path.</param>
        /// <param name="backup">Original report contents.</param>
        /// <returns>True when the report was read successfully.</returns>
        private static bool TryReadCoverageXmlReportBackup(string filePath, out CoverageXmlReportBackup backup)
        {
            try
            {
                backup = new CoverageXmlReportBackup(filePath, File.ReadAllBytes(filePath));
                return true;
            }
            catch (Exception ex)
            {
                backup = default;
                Log.Warning(ex, "RunCiCommand: Coverlet collector XML report could not be backed up before processing: {Path}", filePath);
                return false;
            }
        }

        /// <summary>
        /// Restores coverage XML reports when a multi-report Coverlet collector fallback cannot be completed safely.
        /// </summary>
        /// <param name="backups">Reports captured before processing started.</param>
        private static void TryRestoreCoverageXmlReports(List<CoverageXmlReportBackup> backups)
        {
            foreach (var backup in backups)
            {
                TryRestoreCoverageXmlReport(backup);
            }
        }

        /// <summary>
        /// Restores a coverage XML report through a same-directory temporary file to avoid truncating the original on partial writes.
        /// </summary>
        /// <param name="backup">Report contents captured before fallback processing started.</param>
        private static void TryRestoreCoverageXmlReport(CoverageXmlReportBackup backup)
        {
            var fullPath = Path.GetFullPath(backup.FilePath);
            var directoryPath = Path.GetDirectoryName(fullPath);
            if (StringUtil.IsNullOrWhiteSpace(directoryPath))
            {
                Log.Warning("RunCiCommand: Coverlet collector XML report could not be restored because its directory could not be resolved: {Path}", backup.FilePath);
                return;
            }

            var fileName = Path.GetFileName(fullPath);
            var temporaryPath = Path.Combine(directoryPath!, $".{fileName}.{Guid.NewGuid():N}.restore.tmp");
            var replaceBackupPath = temporaryPath + ".bak";
            Exception? lastException = null;
            for (var attempt = 1; attempt <= CoverageXmlRestoreMaxAttempts; attempt++)
            {
                try
                {
                    File.WriteAllBytes(temporaryPath, backup.OriginalContents);
                    if (File.Exists(fullPath))
                    {
                        File.Replace(temporaryPath, fullPath, replaceBackupPath);
                        TryDeleteFile(replaceBackupPath);
                        return;
                    }

                    File.Move(temporaryPath, fullPath);
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    TryDeleteFile(replaceBackupPath);
                    if (attempt < CoverageXmlRestoreMaxAttempts)
                    {
                        Thread.Sleep(CoverageXmlRestoreRetryDelayMilliseconds * attempt);
                    }
                }
            }

            try
            {
                if (!File.Exists(temporaryPath))
                {
                    File.WriteAllBytes(temporaryPath, backup.OriginalContents);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "RunCiCommand: Coverlet collector XML restore backup could not be retained: {Path}", backup.FilePath);
            }

            Log.Warning(lastException, "RunCiCommand: Coverlet collector XML report could not be restored after processing failed: {Path}. Original bytes were retained at: {TemporaryPath}", backup.FilePath, temporaryPath);
        }

        /// <summary>
        /// Finds current-session Coverlet collector XML attachments in all resolved result directories.
        /// </summary>
        /// <param name="resultsDirectories">VSTest results directory candidates.</param>
        /// <param name="sessionStartTime">Current test session start time.</param>
        /// <returns>Coverage XML attachment paths written during the current session.</returns>
        private static CoverletCollectorXmlReport[] GetCoverletCollectorXmlReports(string[] resultsDirectories, DateTimeOffset sessionStartTime)
        {
            var coverageFiles = new List<CoverletCollectorXmlReport>();
            var seenCoverageFiles = new HashSet<string>(CoverageReportPathComparer);
            var baselineCoverageFiles = TryGetCoverletCollectorXmlReportBaseline(resultsDirectories);
            foreach (var resultsDirectory in resultsDirectories)
            {
                if (Directory.Exists(resultsDirectory))
                {
                    foreach (var coverageReport in GetCoverletCollectorXmlReports(resultsDirectory, sessionStartTime, baselineCoverageFiles))
                    {
                        if (seenCoverageFiles.Add(coverageReport.FilePath))
                        {
                            coverageFiles.Add(coverageReport);
                        }
                    }
                }
            }

            return coverageFiles.ToArray();
        }

        /// <summary>
        /// Finds current-session Coverlet collector XML attachments in one result directory in all backfillable XML formats.
        /// </summary>
        /// <param name="resultsDirectory">VSTest results directory.</param>
        /// <param name="sessionStartTime">Current test session start time.</param>
        /// <param name="baselineCoverageFiles">Coverlet XML attachments that existed before the current session.</param>
        /// <returns>Coverage XML attachment paths written during the current session.</returns>
        private static CoverletCollectorXmlReport[] GetCoverletCollectorXmlReports(string resultsDirectory, DateTimeOffset sessionStartTime, HashSet<string>? baselineCoverageFiles)
        {
            var coverageFiles = new List<CoverletCollectorXmlReport>();
            foreach (var descriptor in CoverletCollectorXmlReports)
            {
                foreach (var filePath in Directory.EnumerateFiles(resultsDirectory, descriptor.FileName, SearchOption.AllDirectories)
                                                  .Where(filePath => IsSessionCoverageAttachment(filePath, sessionStartTime, baselineCoverageFiles)))
                {
                    coverageFiles.Add(new CoverletCollectorXmlReport(filePath, descriptor.Format, descriptor.PublishPriority));
                }
            }

            return coverageFiles.ToArray();
        }

        private static List<List<SelectedCoverletCollectorXmlReport>> SelectCoverletCollectorXmlReportGroupsForProcessing(CoverletCollectorXmlReport[] coverageReports)
        {
            var reportsByAttachmentDirectory = new Dictionary<string, List<SelectedCoverletCollectorXmlReport>>(CoverageReportPathComparer);
            for (var i = 0; i < coverageReports.Length; i++)
            {
                var coverageReport = coverageReports[i];
                var attachmentDirectory = GetAttachmentDirectoryKey(coverageReport.FilePath);
                if (!reportsByAttachmentDirectory.TryGetValue(attachmentDirectory, out var reports))
                {
                    reports = [];
                    reportsByAttachmentDirectory[attachmentDirectory] = reports;
                }

                reports.Add(new SelectedCoverletCollectorXmlReport(coverageReport, i));
            }

            var selectedReportGroups = reportsByAttachmentDirectory.Values
                                                                   .Select(reports => reports.OrderBy(report => report.Report.PublishPriority).ThenBy(report => report.Order).ToList())
                                                                   .OrderBy(reports => reports[0].Order)
                                                                   .ToList();
            return selectedReportGroups;
        }

        private static string GetAttachmentDirectoryKey(string filePath)
        {
            var fullPath = Path.GetFullPath(filePath);
            return Path.GetDirectoryName(fullPath) ?? fullPath;
        }

        private static HashSet<string>? TryGetCoverletCollectorXmlReportBaseline(string[] resultsDirectories)
        {
            var normalizedResultsDirectories = NormalizeCoverageReportPaths(resultsDirectories);
            lock (CoverletCollectorXmlReportBaselineLock)
            {
                if (_coverletCollectorXmlReportBaseline is null ||
                    !AreEquivalentCoverageReportPathSets(_coverletCollectorXmlReportBaseline.ResultsDirectories, normalizedResultsDirectories))
                {
                    return null;
                }

                return _coverletCollectorXmlReportBaseline.FilePaths;
            }
        }

        private static HashSet<string> GetAllCoverletCollectorXmlReportPaths(string[] resultsDirectories)
        {
            var coverageFiles = new HashSet<string>(CoverageReportPathComparer);
            foreach (var resultsDirectory in resultsDirectories)
            {
                if (!Directory.Exists(resultsDirectory))
                {
                    continue;
                }

                foreach (var descriptor in CoverletCollectorXmlReports)
                {
                    foreach (var filePath in Directory.EnumerateFiles(resultsDirectory, descriptor.FileName, SearchOption.AllDirectories))
                    {
                        coverageFiles.Add(GetFullCoverageReportPath(filePath));
                    }
                }
            }

            return coverageFiles;
        }

        private static string[] NormalizeCoverageReportPaths(string[] paths)
        {
            var normalizedPaths = new string[paths.Length];
            for (var i = 0; i < paths.Length; i++)
            {
                normalizedPaths[i] = GetFullCoverageReportPath(paths[i]);
            }

            return normalizedPaths;
        }

        private static bool AreEquivalentCoverageReportPathSets(string[] left, string[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            var pathSet = new HashSet<string>(left, CoverageReportPathComparer);
            foreach (var path in right)
            {
                if (!pathSet.Remove(path))
                {
                    return false;
                }
            }

            return pathSet.Count == 0;
        }

        private static string GetFullCoverageReportPath(string path)
            => Path.GetFullPath(path);

        /// <summary>
        /// Gets the ordered base directories that VSTest may have used for relative and default result paths.
        /// </summary>
        /// <param name="workingDirectory">Session working directory, which may be metadata-only in EVP tests.</param>
        /// <returns>Candidate base directories.</returns>
        private static string[] GetCoverletCollectorBaseDirectories(string? workingDirectory)
        {
            var baseDirectories = new List<string>();
            TryAddDirectory(baseDirectories, GetAbsoluteWorkingDirectory(workingDirectory));

            // The shared run folder is created under the launching process directory. In EVP scenarios the
            // session working directory can be metadata-only, so this gives the XML fallback a stable local base.
            if (TryGetCoverageBackfillRunFolderBaseDirectory(out var runFolderBaseDirectory))
            {
                TryAddDirectory(baseDirectories, runFolderBaseDirectory);
            }

            return baseDirectories.ToArray();
        }

        private static void AddCoverletCollectorResultsDirectories(List<string> resultsDirectories, CoverageBackfillCommandLine command, string baseDirectory)
        {
            if (command.UsesCoverletCollectorCoverage(baseDirectory))
            {
                if (command.TryGetInlineRunSettingsResultsDirectory(out var configuredResultsDirectory))
                {
                    TryAddResolvedDirectory(resultsDirectories, configuredResultsDirectory, baseDirectory);
                }
                else if (command.TryGetOptionValue(CoverletResultsDirectoryOptions, out configuredResultsDirectory))
                {
                    TryAddResolvedDirectory(resultsDirectories, configuredResultsDirectory, baseDirectory);
                }
                else if (command.TryGetVstestResultsDirectory(out configuredResultsDirectory))
                {
                    TryAddResolvedDirectory(resultsDirectories, configuredResultsDirectory, baseDirectory);
                }
                else if (command.TryGetRunSettingsFileResultsDirectory(baseDirectory, out configuredResultsDirectory))
                {
                    TryAddDirectory(resultsDirectories, configuredResultsDirectory);
                }
                else
                {
                    AddCoverletCollectorProjectDefaultResultsDirectories(resultsDirectories, command, baseDirectory);
                    TryAddDirectory(resultsDirectories, Path.Combine(baseDirectory, "TestResults"));
                }
            }

            foreach (var childCommand in command.GetDotnetCoverageCollectChildCommands())
            {
                AddCoverletCollectorResultsDirectories(resultsDirectories, childCommand, baseDirectory);
            }
        }

        /// <summary>
        /// Resolves and adds a directory to a distinct candidate list.
        /// </summary>
        /// <param name="directories">Candidate list to update.</param>
        /// <param name="directoryPath">Directory path to resolve.</param>
        /// <param name="baseDirectory">Base directory for relative paths.</param>
        private static void TryAddResolvedDirectory(List<string> directories, string directoryPath, string baseDirectory)
        {
            if (TryResolveDirectoryPath(directoryPath, baseDirectory, out var resolvedPath))
            {
                TryAddDirectory(directories, resolvedPath);
            }
        }

        /// <summary>
        /// Adds VSTest's project-local default <c>TestResults</c> directory for explicit test project or solution arguments.
        /// </summary>
        /// <param name="directories">Candidate list to update.</param>
        /// <param name="command">Parsed test command.</param>
        /// <param name="baseDirectory">Base directory used to resolve relative project or solution paths.</param>
        private static void AddCoverletCollectorProjectDefaultResultsDirectories(List<string> directories, CoverageBackfillCommandLine command, string baseDirectory)
        {
            var hasExplicitTarget = false;
            foreach (var projectFilePath in command.GetDotnetTestProjectFilePaths())
            {
                hasExplicitTarget = true;
                AddCoverletCollectorProjectDefaultResultsDirectory(directories, projectFilePath, baseDirectory);
            }

            foreach (var solutionFilePath in command.GetDotnetTestSolutionFilePaths())
            {
                hasExplicitTarget = true;
                AddCoverletCollectorSolutionDefaultResultsDirectories(directories, solutionFilePath, baseDirectory);
            }

            foreach (var projectFilePath in command.GetMsBuildProjectFilePaths())
            {
                hasExplicitTarget = true;
                AddCoverletCollectorProjectDefaultResultsDirectory(directories, projectFilePath, baseDirectory);
            }

            foreach (var solutionFilePath in command.GetMsBuildSolutionFilePaths())
            {
                hasExplicitTarget = true;
                AddCoverletCollectorSolutionDefaultResultsDirectories(directories, solutionFilePath, baseDirectory);
            }

            foreach (var targetDirectoryPath in command.GetDotnetTestDirectoryPaths(baseDirectory))
            {
                hasExplicitTarget = true;
                AddCoverletCollectorImplicitDefaultResultsDirectories(directories, targetDirectoryPath);
                TryAddDirectory(directories, Path.Combine(targetDirectoryPath, "TestResults"));
            }

            if (!hasExplicitTarget &&
                (command.UsesImplicitDotnetTestTarget() ||
                 command.UsesImplicitMsBuildVstestTarget()))
            {
                AddCoverletCollectorImplicitDefaultResultsDirectories(directories, baseDirectory);
            }
        }

        private static void AddCoverletCollectorProjectDefaultResultsDirectory(List<string> directories, string projectFilePath, string baseDirectory)
        {
            if (!TryResolveDirectoryPath(projectFilePath, baseDirectory, out var resolvedProjectFilePath))
            {
                return;
            }

            AddCoverletCollectorProjectDefaultResultsDirectory(directories, resolvedProjectFilePath);
        }

        private static void AddCoverletCollectorProjectDefaultResultsDirectory(List<string> directories, string resolvedProjectFilePath)
        {
            var projectDirectory = Path.GetDirectoryName(resolvedProjectFilePath);
            if (!StringUtil.IsNullOrEmpty(projectDirectory))
            {
                TryAddDirectory(directories, Path.Combine(projectDirectory!, "TestResults"));
            }
        }

        private static void AddCoverletCollectorSolutionDefaultResultsDirectories(List<string> directories, string solutionFilePath, string baseDirectory)
        {
            if (!TryResolveDirectoryPath(solutionFilePath, baseDirectory, out var resolvedSolutionFilePath))
            {
                return;
            }

            var solutionDirectory = Path.GetDirectoryName(resolvedSolutionFilePath);
            if (StringUtil.IsNullOrEmpty(solutionDirectory))
            {
                return;
            }

            foreach (var projectFilePath in GetProjectFilePathsFromSolution(resolvedSolutionFilePath))
            {
                if (TryResolveDirectoryPath(NormalizeSolutionProjectPath(projectFilePath), solutionDirectory!, out var resolvedProjectFilePath))
                {
                    AddCoverletCollectorProjectDefaultResultsDirectory(directories, resolvedProjectFilePath);
                }
            }
        }

        private static string[] GetProjectFilePathsFromSolution(string solutionFilePath)
        {
            try
            {
                if (solutionFilePath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
                {
                    return GetProjectFilePathsFromSlnx(solutionFilePath);
                }

                if (solutionFilePath.EndsWith(".slnf", StringComparison.OrdinalIgnoreCase))
                {
                    return GetProjectFilePathsFromSlnf(solutionFilePath);
                }

                if (solutionFilePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                {
                    return GetProjectFilePathsFromSln(solutionFilePath);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "RunCiCommand: Could not inspect solution file for Coverlet collector default result directories: {Path}", solutionFilePath);
            }

            return [];
        }

        private static void AddCoverletCollectorImplicitDefaultResultsDirectories(List<string> directories, string baseDirectory)
        {
            var solutionFilePaths = GetImplicitSolutionFilePaths(baseDirectory);
            if (solutionFilePaths.Length == 1)
            {
                AddCoverletCollectorSolutionDefaultResultsDirectories(directories, solutionFilePaths[0], baseDirectory);
                return;
            }

            if (solutionFilePaths.Length > 1)
            {
                return;
            }

            var projectFilePaths = GetImplicitProjectFilePaths(baseDirectory);
            if (projectFilePaths.Length == 1)
            {
                AddCoverletCollectorProjectDefaultResultsDirectory(directories, projectFilePaths[0], baseDirectory);
            }
        }

        private static string[] GetImplicitSolutionFilePaths(string baseDirectory)
        {
            try
            {
                if (!Directory.Exists(baseDirectory))
                {
                    return [];
                }

                return Directory.GetFiles(baseDirectory, "*.sln", SearchOption.TopDirectoryOnly)
                                .Concat(Directory.GetFiles(baseDirectory, "*.slnf", SearchOption.TopDirectoryOnly))
                                .Concat(Directory.GetFiles(baseDirectory, "*.slnx", SearchOption.TopDirectoryOnly))
                                .ToArray();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "RunCiCommand: Could not inspect implicit dotnet test solution files for Coverlet collector default result directories: {Path}", baseDirectory);
                return [];
            }
        }

        private static string[] GetImplicitProjectFilePaths(string baseDirectory)
        {
            try
            {
                if (!Directory.Exists(baseDirectory))
                {
                    return [];
                }

                return Directory.GetFiles(baseDirectory, "*.csproj", SearchOption.TopDirectoryOnly)
                                .Concat(Directory.GetFiles(baseDirectory, "*.fsproj", SearchOption.TopDirectoryOnly))
                                .Concat(Directory.GetFiles(baseDirectory, "*.vbproj", SearchOption.TopDirectoryOnly))
                                .ToArray();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "RunCiCommand: Could not inspect implicit dotnet test project files for Coverlet collector default result directories: {Path}", baseDirectory);
                return [];
            }
        }

        private static string[] GetProjectFilePathsFromSln(string solutionFilePath)
        {
            var projectFilePaths = new List<string>();
            foreach (var line in File.ReadLines(solutionFilePath))
            {
                if (TryGetSlnProjectPath(line, out var projectFilePath) &&
                    IsDotnetProjectFilePath(projectFilePath))
                {
                    projectFilePaths.Add(projectFilePath);
                }
            }

            return projectFilePaths.ToArray();
        }

        private static string[] GetProjectFilePathsFromSlnf(string solutionFilterFilePath)
        {
            var projectFilePaths = new List<string>();
            var solutionFilter = JsonHelper.DeserializeObject<SolutionFilterFile>(File.ReadAllText(solutionFilterFilePath));
            if (solutionFilter?.Solution?.Projects is not { } projects)
            {
                return [];
            }

            var projectBaseDirectory = GetSlnfProjectBaseDirectory(solutionFilterFilePath, solutionFilter.Solution.Path);
            foreach (var projectFilePath in projects)
            {
                if (StringUtil.IsNullOrWhiteSpace(projectFilePath) ||
                    !IsDotnetProjectFilePath(projectFilePath!))
                {
                    continue;
                }

                projectFilePaths.Add(Path.IsPathRooted(projectFilePath!) ? projectFilePath! : Path.Combine(projectBaseDirectory, NormalizeSolutionProjectPath(projectFilePath!)));
            }

            return projectFilePaths.ToArray();
        }

        private static string GetSlnfProjectBaseDirectory(string solutionFilterFilePath, string? solutionPath)
        {
            var solutionFilterDirectory = Path.GetDirectoryName(solutionFilterFilePath) ?? ".";
            if (StringUtil.IsNullOrWhiteSpace(solutionPath))
            {
                return solutionFilterDirectory;
            }

            var normalizedSolutionPath = NormalizeSolutionProjectPath(solutionPath!);
            var resolvedSolutionPath = Path.IsPathRooted(normalizedSolutionPath) ? normalizedSolutionPath : Path.Combine(solutionFilterDirectory, normalizedSolutionPath);
            return Path.GetDirectoryName(resolvedSolutionPath) ?? solutionFilterDirectory;
        }

        private static string[] GetProjectFilePathsFromSlnx(string solutionFilePath)
        {
            var projectFilePaths = new List<string>();
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            var xmlDocument = new XmlDocument
            {
                XmlResolver = null
            };
            using var reader = XmlReader.Create(solutionFilePath, settings);
            xmlDocument.Load(reader);

            var projectNodes = xmlDocument.SelectNodes("//*[translate(local-name(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='project']");
            if (projectNodes is null)
            {
                return [];
            }

            foreach (XmlNode? projectNode in projectNodes)
            {
                var projectFilePath = projectNode?.Attributes?["Path"]?.Value;
                if (!StringUtil.IsNullOrEmpty(projectFilePath) &&
                    IsDotnetProjectFilePath(projectFilePath!))
                {
                    projectFilePaths.Add(projectFilePath!);
                }
            }

            return projectFilePaths.ToArray();
        }

        private static bool TryGetSlnProjectPath(string line, out string projectFilePath)
        {
            projectFilePath = string.Empty;
            var equalsIndex = line.IndexOf('=');
            if (equalsIndex < 0)
            {
                return false;
            }

            var searchIndex = equalsIndex + 1;
            if (!TryReadQuotedSlnField(line, ref searchIndex, out _) ||
                !TryReadQuotedSlnField(line, ref searchIndex, out projectFilePath))
            {
                return false;
            }

            return !StringUtil.IsNullOrEmpty(projectFilePath);
        }

        private static bool TryReadQuotedSlnField(string line, ref int searchIndex, out string value)
        {
            value = string.Empty;
            var startIndex = line.IndexOf('"', searchIndex);
            if (startIndex < 0)
            {
                return false;
            }

            var endIndex = line.IndexOf('"', startIndex + 1);
            if (endIndex < 0)
            {
                return false;
            }

            value = line.Substring(startIndex + 1, endIndex - startIndex - 1);
            searchIndex = endIndex + 1;
            return true;
        }

        private static bool IsDotnetProjectFilePath(string filePath)
        {
            return filePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeSolutionProjectPath(string filePath)
        {
            return Path.DirectorySeparatorChar == '/' ? filePath.Replace('\\', '/') : filePath.Replace('/', '\\');
        }

        /// <summary>
        /// Adds a directory to a candidate list, preserving order and avoiding duplicates.
        /// </summary>
        /// <param name="directories">Candidate list to update.</param>
        /// <param name="directoryPath">Directory path to add.</param>
        private static void TryAddDirectory(List<string> directories, string? directoryPath)
        {
            if (StringUtil.IsNullOrEmpty(directoryPath) ||
                directories.Contains(directoryPath!, Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal))
            {
                return;
            }

            directories.Add(directoryPath!);
        }

        /// <summary>
        /// Gets the base directory that contains the shared <c>.dd</c> backfill run folder, when available.
        /// </summary>
        /// <param name="baseDirectory">Resolved parent directory of the <c>.dd</c> folder.</param>
        /// <returns>True when the run folder follows the expected <c>base/.dd/run-id</c> shape.</returns>
        private static bool TryGetCoverageBackfillRunFolderBaseDirectory(out string baseDirectory)
        {
            baseDirectory = string.Empty;
            var runFolder = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder);
            if (StringUtil.IsNullOrEmpty(runFolder))
            {
                return false;
            }

            try
            {
                var runDirectory = new DirectoryInfo(Path.GetFullPath(runFolder!));
                var dotDatadogDirectory = runDirectory.Parent;
                if (dotDatadogDirectory is null ||
                    !dotDatadogDirectory.Name.Equals(".dd", StringComparison.OrdinalIgnoreCase) ||
                    dotDatadogDirectory.Parent is null)
                {
                    return false;
                }

                baseDirectory = dotDatadogDirectory.Parent.FullName;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the current process start time for current-run coverage attachment filtering.
        /// </summary>
        /// <returns>Process start time, or the minimum timestamp when the runtime does not expose it.</returns>
        private static DateTimeOffset GetCurrentProcessStartTime()
        {
            try
            {
                return new DateTimeOffset(System.Diagnostics.Process.GetCurrentProcess().StartTime).ToUniversalTime();
            }
            catch
            {
                return DateTimeOffset.MinValue;
            }
        }

        /// <summary>
        /// Checks whether a configured coverage report output was written during the current test session.
        /// </summary>
        /// <param name="filePath">Configured coverage report path.</param>
        /// <param name="sessionStartTime">Current test session start time.</param>
        /// <returns>True when the report was modified recently enough to belong to this session.</returns>
        private static bool IsCoverageReportWrittenDuringSession(string filePath, DateTimeOffset sessionStartTime)
        {
            try
            {
                var sessionStartUtc = sessionStartTime.UtcDateTime - CoverletCollectorXmlReportTimestampTolerance;
                return File.GetLastWriteTimeUtc(filePath) >= sessionStartUtc;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks whether a Coverlet collector attachment was written during the current test session.
        /// </summary>
        /// <param name="filePath">Coverage attachment path.</param>
        /// <param name="sessionStartTime">Current test session start time.</param>
        /// <param name="baselineCoverageFiles">Coverlet XML attachments that existed before the current session.</param>
        /// <returns>True when the file timestamp is new enough to belong to this session.</returns>
        private static bool IsSessionCoverageAttachment(string filePath, DateTimeOffset sessionStartTime, HashSet<string>? baselineCoverageFiles)
        {
            try
            {
                if (baselineCoverageFiles?.Contains(GetFullCoverageReportPath(filePath)) == true)
                {
                    return false;
                }

                var sessionStartUtc = sessionStartTime.UtcDateTime - CoverletCollectorXmlReportTimestampTolerance;
                return File.GetCreationTimeUtc(filePath) >= sessionStartUtc ||
                       File.GetLastWriteTimeUtc(filePath) >= sessionStartUtc;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Resolves a working directory to an absolute path while preserving the propagated command directory when available.
        /// </summary>
        /// <param name="workingDirectory">Session working directory, which may have been normalized to a repository-relative path.</param>
        /// <returns>Absolute directory used as the base for VSTest result paths.</returns>
        private static string GetAbsoluteWorkingDirectory(string? workingDirectory)
        {
            var propagatedWorkingDirectory = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
            if (!StringUtil.IsNullOrEmpty(propagatedWorkingDirectory) && Path.IsPathRooted(propagatedWorkingDirectory!))
            {
                return propagatedWorkingDirectory!;
            }

            if (IsForeignWindowsRootedPath(propagatedWorkingDirectory) ||
                IsForeignWindowsRootedPath(workingDirectory))
            {
                return Environment.CurrentDirectory;
            }

            if (!StringUtil.IsNullOrEmpty(workingDirectory))
            {
                if (Path.IsPathRooted(workingDirectory!))
                {
                    return Path.GetFullPath(workingDirectory!);
                }

                if (!StringUtil.IsNullOrEmpty(TestOptimization.Instance.CIValues.WorkspacePath))
                {
                    return Path.GetFullPath(Path.Combine(TestOptimization.Instance.CIValues.WorkspacePath!, workingDirectory!));
                }
            }

            return Environment.CurrentDirectory;
        }

        /// <summary>
        /// Checks whether a Windows-rooted path is being interpreted by a non-Windows runtime.
        /// </summary>
        /// <param name="path">Candidate working directory path.</param>
        /// <returns>True when the path is rooted for Windows but not for the current platform.</returns>
        private static bool IsForeignWindowsRootedPath(string? path)
        {
            return Path.DirectorySeparatorChar != '\\' &&
                   path is { Length: > 2 } &&
                   char.IsLetter(path[0]) &&
                   path[1] == ':' &&
                   path[2] is '\\' or '/';
        }

        /// <summary>
        /// Gets the normalized child test command used for coverage backfill internals without changing the public test session command tag.
        /// </summary>
        /// <param name="fallbackCommandLine">Public test session command used when no internal command was propagated.</param>
        /// <returns>Command line used to discover coverage tool outputs.</returns>
        private static string? GetCoverageBackfillCommandLine(string? fallbackCommandLine)
        {
            return EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand) ?? fallbackCommandLine;
        }

        private static string? GetInjectedSessionCoverageBackfillCommandLine()
        {
            if (EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand) is { Length: > 0 } commandLine)
            {
                return GetCoverageBackfillCommandLine(commandLine);
            }

            return GetCoverageBackfillCommandLine(Environment.CommandLine);
        }

        private static string GetInjectedSessionCoverageBackfillWorkingDirectory()
        {
            if (EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory) is { Length: > 0 } workingDirectory)
            {
                return workingDirectory;
            }

            return Environment.CurrentDirectory;
        }

        private static bool IsCoverletCollectorXmlFallbackCommand(string? commandLine, string? workingDirectory)
        {
            return GetCoverletCollectorResultsDirectories(commandLine, workingDirectory).Length > 0;
        }

        private static void SetInjectedSessionCoverletXmlFallbackEnabled(bool enabled)
        {
            lock (CoverletCollectorXmlReportBaselineLock)
            {
                _injectedSessionCoverletXmlFallbackEnabled = enabled;
            }
        }

        private static bool TryConsumeInjectedSessionCoverletXmlFallbackEnabled()
        {
            lock (CoverletCollectorXmlReportBaselineLock)
            {
                var enabled = _injectedSessionCoverletXmlFallbackEnabled &&
                              HasExplicitInjectedSessionCoverletXmlFallbackContext();
                _injectedSessionCoverletXmlFallbackEnabled = false;
                return enabled;
            }
        }

        private static bool HasExplicitInjectedSessionCoverletXmlFallbackContext()
        {
            return !StringUtil.IsNullOrWhiteSpace(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder)) &&
                   !StringUtil.IsNullOrWhiteSpace(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand));
        }

        /// <summary>
        /// Resolves a configured directory path against the command working directory.
        /// </summary>
        /// <param name="directoryPath">Directory path from the command line.</param>
        /// <param name="baseDirectory">Absolute command working directory.</param>
        /// <param name="resolvedPath">Resolved absolute path.</param>
        /// <returns>True when the path could be resolved.</returns>
        private static bool TryResolveDirectoryPath(string directoryPath, string baseDirectory, out string resolvedPath)
        {
            resolvedPath = string.Empty;
            try
            {
                resolvedPath = Path.IsPathRooted(directoryPath) ? Path.GetFullPath(directoryPath) : Path.GetFullPath(Path.Combine(baseDirectory, directoryPath));
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets backend ITR coverage data when it is available for the current process.
        /// </summary>
        /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
        /// <returns>True when backfill data can be safely used by an in-process coverage tool adapter.</returns>
        internal static bool TryGetCoverageBackfillDataForCurrentProcess(out CoverageBackfillData backfillData)
        {
            return GetCoverageBackfillDataForCurrentProcess(out backfillData) == CoverageBackfillAvailability.Available;
        }

        internal static bool TryGetCoverageBackfillDataForCurrentProcess(ulong sessionId, out CoverageBackfillData backfillData)
        {
            return GetCoverageBackfillDataForCurrentProcess(sessionId, out backfillData) == CoverageBackfillAvailability.Available;
        }

        internal static bool TryGetCoverageBackfillDataForCurrentProcess(ulong sessionId, out CoverageBackfillData backfillData, out bool unavailableAfterActualItrSkip)
        {
            var availability = GetCoverageBackfillDataForCurrentProcess(sessionId, out backfillData);
            unavailableAfterActualItrSkip = availability == CoverageBackfillAvailability.Unavailable;
            return availability == CoverageBackfillAvailability.Available;
        }

        private static CoverageBackfillAvailability GetCoverageBackfillDataForCurrentProcess(out CoverageBackfillData backfillData)
            => GetCoverageBackfillDataForCurrentProcess(sessionId: 0, out backfillData);

        private static CoverageBackfillAvailability GetCoverageBackfillDataForCurrentProcess(ulong sessionId, out CoverageBackfillData backfillData)
        {
            backfillData = CoverageBackfillData.Missing;
            if (!HasActualItrSkipsForCurrentProcess(sessionId))
            {
                return CoverageBackfillAvailability.NotRequired;
            }

            var skippableFeature = TestOptimization.Instance.SkippableFeature;
            if (skippableFeature?.IsCoverageBackfillSafe() == true)
            {
                backfillData = skippableFeature.GetCoverageBackfillData();
                if (backfillData is { IsPresent: true, IsValid: true })
                {
                    return CoverageBackfillAvailability.Available;
                }
            }

            var loaded = sessionId == 0 ?
                             CoverageBackfillDataStore.TryLoad(out backfillData) :
                             CoverageBackfillDataStore.TryLoad(TestOptimization.Instance, sessionId, out backfillData);
            return loaded ?
                       CoverageBackfillAvailability.Available :
                       CoverageBackfillAvailability.Unavailable;
        }

        private static bool HasActualItrSkipsForCurrentProcess(ulong sessionId)
        {
            if (sessionId == 0)
            {
                return CoverageBackfillDataStore.HasActualItrSkip();
            }

            var skippableFeature = TestOptimization.Instance.SkippableFeature;
            return skippableFeature?.HasSkippedTestsByItr(sessionId) == true ||
                   HasPersistedActualItrSkipForSessionOrLegacy(sessionId);
        }

        private static bool HasActualItrSkips()
        {
            // This marker is written only when a framework skip decision came from the skippable-tests response.
            // Session/module ITR tags can also be set by historical test fixtures that use the ITR skip reason directly.
            return CoverageBackfillDataStore.HasActualItrSkip();
        }

        private static bool HasActualItrSkips(TestSession? session)
        {
            if (session is null)
            {
                return HasActualItrSkips();
            }

            var skippableFeature = TestOptimization.Instance.SkippableFeature;
            return skippableFeature is null ?
                       HasActualItrSkips() :
                       skippableFeature.HasSkippedTestsByItr(session.Tags.SessionId) ||
                       HasPersistedActualItrSkipForSessionOrLegacy(session.Tags.SessionId);
        }

        private static bool HasPersistedActualItrSkipForSessionOrLegacy(ulong sessionId)
            => CoverageBackfillDataStore.HasPersistedActualItrSkipForSessionOrLegacy(TestOptimization.Instance, sessionId);

        private static CoverageBackfillAvailability GetCoverageBackfillDataForSession(TestSession session, out CoverageBackfillData backfillData)
        {
            backfillData = CoverageBackfillData.Missing;
            if (!HasActualItrSkips(session))
            {
                return CoverageBackfillAvailability.NotRequired;
            }

            var skippableFeature = TestOptimization.Instance.SkippableFeature;
            if (skippableFeature?.IsCoverageBackfillSafe() == true)
            {
                backfillData = skippableFeature.GetCoverageBackfillData();
                if (backfillData is { IsPresent: true, IsValid: true })
                {
                    return CoverageBackfillAvailability.Available;
                }
            }

            return CoverageBackfillDataStore.TryLoad(TestOptimization.Instance, session.Tags.SessionId, out backfillData) ?
                       CoverageBackfillAvailability.Available :
                       CoverageBackfillAvailability.Unavailable;
        }

        internal static bool TryGetCoveragePercentageFromXml(string filePath, out double percentage)
        {
            if (File.Exists(filePath) &&
                ExternalCoverageXmlBackfill.TryProcess(filePath, backfillData: null, applyBackfill: false, out var result))
            {
                percentage = result.Percentage;
                Log.Debug("TryGetCoveragePercentageFromXml: {Diagnostic} code coverage was reported: {Value}", result.Diagnostic, result.Percentage);
                return true;
            }

            percentage = 0;
            return false;
        }

        internal static void InjectCodeCoverageCollectorToDotnetTest(ref IEnumerable<string>? msbuildArgs)
        {
            if (msbuildArgs == null)
            {
                return;
            }

            // In this case a single property contains multiple values separated by a semi colon
            const string collectProperty = "-property:VSTestCollect=";
            const string datadogCoverageCollector = "DatadogCoverage";
            const string testAdapterPathProperty = "-property:VSTestTestAdapterPath=";

            var disableTestAdapterInjection = false;
            var codeCoverageCollectorPath = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath) ?? string.Empty;
            if (StringUtil.IsNullOrEmpty(codeCoverageCollectorPath))
            {
                Log.Warning("InjectCodeCoverageCollector.DotnetTest: The tracer home directory cannot be found based on the DD_CIVISIBILITY_CODE_COVERAGE_COLLECTORPATH value. TestAdapterPath will not be injected.");
                disableTestAdapterInjection = true;
            }

            var isCollectIndex = -1;
            var isTestAdapterPathIndex = -1;
            var msbuildArgsList = msbuildArgs as List<string> ?? [..msbuildArgs];
            for (var i = 0; i < msbuildArgsList.Count; i++)
            {
                var arg = msbuildArgsList[i];
                if (arg.StartsWith(collectProperty, StringComparison.OrdinalIgnoreCase))
                {
                    isCollectIndex = i;
                    continue;
                }

                if (arg.StartsWith(testAdapterPathProperty, StringComparison.OrdinalIgnoreCase))
                {
                    isTestAdapterPathIndex = i;
                }
            }

            if (CoverageBackfillCapability.IsTestingPlatformCoverageCommand(BuildDotnetTestCommandLine(msbuildArgsList), Environment.CurrentDirectory) &&
                !CollectPropertyContainsDatadogCoverage(msbuildArgsList, isCollectIndex, collectProperty, datadogCoverageCollector))
            {
                Log.Information("InjectCodeCoverageCollector.DotnetTest: Datadog data collector was not added because Microsoft Testing Platform coverage is already selected.");
                msbuildArgs = msbuildArgsList;
                return;
            }

            if (isCollectIndex == -1)
            {
                // Add the collect property
                Log.Information("InjectCodeCoverageCollector.DotnetTest: Adding the collect property with the Datadog data collector.");
                msbuildArgsList.Add($"{collectProperty}\"{datadogCoverageCollector}\"");
            }
            else
            {
                // Modify the collect property
                var item = msbuildArgsList[isCollectIndex];
                Log.Debug("InjectCodeCoverageCollector.DotnetTest: Existing raw collect property values: {CollectProperty}", item);
                var cleanItem = item.Substring(collectProperty.Length)
                                    .Replace("\"", string.Empty);
                Log.Debug("InjectCodeCoverageCollector.DotnetTest: Existing clean collect property values: {CollectProperty}", cleanItem);
                var values = cleanItem.Split(Separators.SemiColon, StringSplitOptions.None);

                if (!values.Contains(datadogCoverageCollector, StringComparer.OrdinalIgnoreCase))
                {
                    Log.Information("InjectCodeCoverageCollector.DotnetTest: Appending the Datadog data collector.");
                    item = $"{collectProperty}\"{string.Join(";", values.Concat(datadogCoverageCollector))}\"";
                    msbuildArgsList[isCollectIndex] = item;
                }
                else
                {
                    Log.Information("InjectCodeCoverageCollector.DotnetTest: Datadog data collector is already in the collector enumerable.");
                }
            }

            if (!disableTestAdapterInjection)
            {
                if (isTestAdapterPathIndex == -1)
                {
                    // Add the test adapter path property
                    Log.Information("InjectCodeCoverageCollector.DotnetTest: Adding the test adapter path property with the Datadog data collector folder path.");
                    msbuildArgsList.Add($"{testAdapterPathProperty}\"{codeCoverageCollectorPath}\"");
                }
                else
                {
                    // Modify the test adapter path property
                    var item = msbuildArgsList[isTestAdapterPathIndex];
                    var cleanItem = item.Substring(testAdapterPathProperty.Length)
                                        .Replace("\"", string.Empty);
                    Log.Debug("InjectCodeCoverageCollector.DotnetTest: Existing testAdapter property values: {CollectProperty}", cleanItem);
                    var values = cleanItem.Split(Separators.SemiColon, StringSplitOptions.None);

                    if (!values.Contains(codeCoverageCollectorPath))
                    {
                        Log.Information("InjectCodeCoverageCollector.DotnetTest: Appending the Datadog data collector folder path.");
                        item = $"{testAdapterPathProperty}\"{string.Join(";", values.Concat(codeCoverageCollectorPath))}\"";
                        msbuildArgsList[isTestAdapterPathIndex] = item;
                    }
                    else
                    {
                        Log.Information("InjectCodeCoverageCollector.DotnetTest: Datadog data collector path is already in the test adapter path enumerable.");
                    }
                }
            }

            // Replace the msbuildArgs with the modified list
            msbuildArgs = msbuildArgsList;
        }

        private static bool CollectPropertyContainsDatadogCoverage(List<string> msbuildArgs, int collectIndex, string collectProperty, string datadogCoverageCollector)
        {
            if (collectIndex < 0 || collectIndex >= msbuildArgs.Count)
            {
                return false;
            }

            var item = msbuildArgs[collectIndex];
            var cleanItem = item.Substring(collectProperty.Length)
                                .Replace("\"", string.Empty);
            var values = cleanItem.Split(Separators.SemiColon, StringSplitOptions.None);
            return values.Contains(datadogCoverageCollector, StringComparer.OrdinalIgnoreCase);
        }

        private static string BuildDotnetTestCommandLine(List<string> msbuildArgs)
        {
            var builder = new StringBuilder("dotnet test");
            foreach (var argument in msbuildArgs)
            {
                builder.Append(' ');
                builder.Append(QuoteCommandLineArgument(argument));
            }

            return builder.ToString();
        }

        private static string QuoteCommandLineArgument(string argument)
        {
            if (StringUtil.IsNullOrEmpty(argument))
            {
                return "\"\"";
            }

            if (argument.IndexOfAny(CommandLineQuoteCharacters) < 0)
            {
                return argument;
            }

            return $"\"{argument.Replace("\"", "\\\"")}\"";
        }

        internal static void WriteDebugInfoForDotnetTest(IEnumerable<string>? msbuildArgs, IEnumerable<string>? userDefinedArguments, IEnumerable<string>? trailingArguments, bool noRestore, string? msbuildPath)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                var sb = StringBuilderCache.Acquire();
                sb.AppendLine("InjectCodeCoverageCollector.DotnetTest: Microsoft.DotNet.Tools.Test.TestCommand..ctor arguments:");

                if (msbuildArgs is not null)
                {
                    sb.AppendLine("\tmsbuildArgs: ");
                    foreach (var arg in msbuildArgs)
                    {
                        sb.AppendLine($"\t\t{arg}");
                    }
                }

                if (userDefinedArguments is not null)
                {
                    sb.AppendLine("\tuserDefinedArguments: ");
                    foreach (var arg in userDefinedArguments)
                    {
                        sb.AppendLine($"\t\t{arg}");
                    }
                }

                if (trailingArguments is not null)
                {
                    sb.AppendLine("\ttrailingArguments: ");
                    foreach (var arg in trailingArguments)
                    {
                        sb.AppendLine($"\t\t{arg}");
                    }
                }

                sb.AppendLine("\tnoRestore: " + noRestore);
                sb.AppendLine("\tmsbuildPath: " + msbuildPath);
                Log.Debug("{MessageValue}", StringBuilderCache.GetStringAndRelease(sb));
            }
        }

        internal static void InjectCodeCoverageCollectorToVsConsoleTest(ref string[]? args)
        {
            if (args == null)
            {
                return;
            }

            // In this case each property contains just a single value, for multiple values we need to add multiple arguments
            const string collectProperty = "/Collect:";
            const string datadogCoverageCollector = "DatadogCoverage";
            const string testAdapterPathProperty = "/TestAdapterPath:";

            var disableCollectInjection = false;
            var disableTestAdapterInjection = false;
            var codeCoverageCollectorPath = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath) ?? string.Empty;
            if (StringUtil.IsNullOrEmpty(codeCoverageCollectorPath))
            {
                Log.Warning("InjectCodeCoverageCollector.VsConsoleTest:: The tracer home directory cannot be found based on the DD_CIVISIBILITY_CODE_COVERAGE_COLLECTORPATH value. TestAdapterPath will not be injected.");
                disableTestAdapterInjection = true;
            }

            var lstArgs = new List<string>(args);

            UpdateIndexes(lstArgs, codeCoverageCollectorPath, out var isCollectIndex, out var isTestAdapterPathIndex, out var doubleDashIndex, ref disableCollectInjection, ref disableTestAdapterInjection);
            if (disableCollectInjection && disableTestAdapterInjection)
            {
                // Nothing to modify
                return;
            }

            if (!disableTestAdapterInjection)
            {
                Inject(lstArgs, $"{testAdapterPathProperty}\"{codeCoverageCollectorPath}\"", doubleDashIndex, isTestAdapterPathIndex);
                UpdateIndexes(lstArgs, codeCoverageCollectorPath, out isCollectIndex, out isTestAdapterPathIndex, out doubleDashIndex, ref disableCollectInjection, ref disableTestAdapterInjection);
            }

            if (!disableCollectInjection)
            {
                Inject(lstArgs, $"{collectProperty}{datadogCoverageCollector}", doubleDashIndex, isCollectIndex);
            }

            args = lstArgs.ToArray();

            static void UpdateIndexes(List<string> lstArgs, string codeCoverageCollectorPath, out int isCollectIndex, out int isTestAdapterPathIndex, out int doubleDashIndex, ref bool disableCollectInjection, ref bool disableTestAdapterInjection)
            {
                isCollectIndex = -1;
                isTestAdapterPathIndex = -1;
                doubleDashIndex = int.MaxValue;
                for (var i = 0; i < lstArgs.Count; i++)
                {
                    var arg = lstArgs[i];
                    if (arg == "--" && doubleDashIndex > i)
                    {
                        doubleDashIndex = i;
                        continue;
                    }

                    if (!disableCollectInjection && arg.StartsWith(collectProperty, StringComparison.OrdinalIgnoreCase))
                    {
                        isCollectIndex = i;
                        var argValue = arg.Substring(collectProperty.Length)
                                          .Replace("\"", string.Empty);
                        disableCollectInjection = disableCollectInjection || argValue.Equals(datadogCoverageCollector, StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    if (!disableTestAdapterInjection && arg.StartsWith(testAdapterPathProperty, StringComparison.OrdinalIgnoreCase))
                    {
                        isTestAdapterPathIndex = i;
                        var argValue = arg.Substring(testAdapterPathProperty.Length)
                                          .Replace("\"", string.Empty);
                        disableTestAdapterInjection = disableTestAdapterInjection || argValue == codeCoverageCollectorPath;
                    }
                }
            }

            static void Inject(List<string> lstArgs, string value, int doubleDashIndex, int propertyIndex)
            {
                Log.Debug<string, int, int>("InjectCodeCoverageCollector.VsConsoleTest: [ Value={Value} | DoubleDash={DoubleDash} | PropertyIndex={PropertyIndex} ]", value, doubleDashIndex, propertyIndex);
                if (propertyIndex != -1 && propertyIndex < doubleDashIndex)
                {
                    // If there's already an existing property index we insert it just after that one.
                    Log.Information("InjectCodeCoverageCollector.VsConsoleTest: There's already an existing property index we insert it just after that one.");
                    lstArgs.Insert(propertyIndex + 1, value);
                }
                else if (doubleDashIndex < lstArgs.Count)
                {
                    // If we found an arg with "--" we insert it before that arg (everything after is considered as test settings).
                    Log.Information("InjectCodeCoverageCollector.VsConsoleTest: We found an arg with \"--\" we insert it before that arg (everything after is considered as test settings).");
                    lstArgs.Insert(doubleDashIndex, value);
                }
                else
                {
                    // If we don't have neither the "--" nor any property index, we just append the property to the current arg list.
                    Log.Information("InjectCodeCoverageCollector.VsConsoleTest: We don't have neither the \"--\" nor a property index, we just append the property to the current arg list.");
                    lstArgs.Add(value);
                }
            }
        }

        internal static void WriteDebugInfoForVsConsoleTest(string[]? args)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                var sb = StringBuilderCache.Acquire();
                sb.AppendLine("InjectCodeCoverageCollector.VsConsoleTest: arguments:");

                if (args != null)
                {
                    for (var i = 0; i < args.Length; i++)
                    {
                        sb.AppendLine($"\t{i}:{args[i]}");
                    }
                }

                Log.Debug("{MessageValue}", StringBuilderCache.GetStringAndRelease(sb));
            }
        }

        /// <summary>
        /// Describes one Coverlet collector XML attachment format supported by fallback processing.
        /// </summary>
        private readonly struct CoverletCollectorXmlReportDescriptor
        {
            public CoverletCollectorXmlReportDescriptor(string fileName, CoverletCollectorXmlReportFormat format, int publishPriority)
            {
                FileName = fileName;
                Format = format;
                PublishPriority = publishPriority;
            }

            public string FileName { get; }

            public CoverletCollectorXmlReportFormat Format { get; }

            public int PublishPriority { get; }
        }

        /// <summary>
        /// Represents one discovered Coverlet collector XML attachment.
        /// </summary>
        private readonly struct CoverletCollectorXmlReport
        {
            public CoverletCollectorXmlReport(string filePath, CoverletCollectorXmlReportFormat format, int publishPriority)
            {
                FilePath = filePath;
                Format = format;
                PublishPriority = publishPriority;
            }

            public string FilePath { get; }

            public CoverletCollectorXmlReportFormat Format { get; }

            public int PublishPriority { get; }
        }

        /// <summary>
        /// Represents one selected Coverlet collector XML attachment and its discovery order.
        /// </summary>
        private readonly struct SelectedCoverletCollectorXmlReport
        {
            public SelectedCoverletCollectorXmlReport(CoverletCollectorXmlReport report, int order)
            {
                Report = report;
                Order = order;
            }

            public CoverletCollectorXmlReport Report { get; }

            public int Order { get; }
        }

        /// <summary>
        /// Represents one processed Coverlet collector XML attachment and its publication order.
        /// </summary>
        private readonly struct ProcessedCoverletCollectorXmlReport
        {
            public ProcessedCoverletCollectorXmlReport(CoverletCollectorXmlReport report, ExternalCoverageXmlResult result, ExternalCoverageXmlBackfill.CoverageBackfillValidationState validationState, int order, CoverageXmlReportBackup backup)
            {
                Report = report;
                Result = result;
                ValidationState = validationState;
                Order = order;
                Backup = backup;
            }

            public CoverletCollectorXmlReport Report { get; }

            public ExternalCoverageXmlResult Result { get; }

            public ExternalCoverageXmlBackfill.CoverageBackfillValidationState ValidationState { get; }

            public int Order { get; }

            public CoverageXmlReportBackup Backup { get; }
        }

        /// <summary>
        /// Holds the original contents of one coverage XML report so multi-report fallback processing can be rolled back.
        /// </summary>
        private readonly struct CoverageXmlReportBackup
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="CoverageXmlReportBackup"/> struct.
            /// </summary>
            /// <param name="filePath">Coverage XML report path.</param>
            /// <param name="originalContents">Original report bytes captured before processing.</param>
            public CoverageXmlReportBackup(string filePath, byte[] originalContents)
            {
                FilePath = filePath;
                OriginalContents = originalContents;
            }

            /// <summary>
            /// Gets the coverage XML report path.
            /// </summary>
            public string FilePath { get; }

            /// <summary>
            /// Gets the original report bytes captured before processing.
            /// </summary>
            public byte[] OriginalContents { get; }
        }

        /// <summary>
        /// Tracks Coverlet collector XML reports that existed before the current test session started.
        /// </summary>
        private sealed class CoverletCollectorXmlReportBaseline
        {
            public CoverletCollectorXmlReportBaseline(string[] resultsDirectories, HashSet<string> filePaths)
            {
                ResultsDirectories = resultsDirectories;
                FilePaths = filePaths;
            }

            public string[] ResultsDirectories { get; }

            public HashSet<string> FilePaths { get; }
        }

        /// <summary>
        /// Represents the root object in a Visual Studio solution filter file.
        /// </summary>
        private sealed class SolutionFilterFile
        {
            /// <summary>
            /// Gets or sets the filtered solution metadata.
            /// </summary>
            public SolutionFilterSolution? Solution { get; set; }
        }

        /// <summary>
        /// Represents the solution and project list from a Visual Studio solution filter file.
        /// </summary>
        private sealed class SolutionFilterSolution
        {
            /// <summary>
            /// Gets or sets the referenced solution path.
            /// </summary>
            public string? Path { get; set; }

            /// <summary>
            /// Gets or sets the project paths included by the solution filter.
            /// </summary>
            public string[]? Projects { get; set; }
        }
    }
}
