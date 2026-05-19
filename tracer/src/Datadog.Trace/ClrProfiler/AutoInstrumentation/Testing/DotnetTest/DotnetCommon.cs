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
using System.Text;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.Ci.Coverage.Models.Global;
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

        internal static readonly IDatadogLogger Log = TestOptimization.Instance.Log;

        /// <summary>
        /// VSTest result-directory option names accepted by dotnet test and dotnet vstest command lines.
        /// </summary>
        private static readonly string[] CoverletResultsDirectoryOptions = ["--results-directory", "--ResultsDirectory", "/ResultsDirectory"];

        private static bool? _isDataCollectorDomainCache;
        private static bool? _isMsBuildTaskCache;

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
            // We create test session if not DataCollector
            if (IsDataCollectorDomain)
            {
                return null;
            }

            // Let's detect if we already have a session for this test process
            var context = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(
                EnvironmentHelpers.GetEnvironmentVariables(),
                new DictionaryGetterAndSetter(DictionaryGetterAndSetter.EnvironmentVariableKeyProcessor));

            if (context.SpanContext is not null)
            {
                // Session found in the environment variables
                // let's bail-out
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

                var session = TestSession.GetOrCreate(commandLine, workingDirectory, null, null, true);
                CoverageBackfillDataStore.GetOrCreateRunFolder(testOptimization);
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

        internal static void FinalizeSession(TestSession? session, int exitCode, Exception? exception)
        {
            if (session is null)
            {
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
            if (!string.IsNullOrEmpty(codeCoveragePath))
            {
                var outputPath = Path.Combine(codeCoveragePath, $"session-coverage-{DateTime.Now:yyyy-MM-dd_HH_mm_ss}.json");
                if (CoverageUtils.TryCombineAndGetTotalCoverage(codeCoveragePath, outputPath, out var globalCoverage) &&
                    globalCoverage is not null)
                {
                    var backfillResult = TryApplyItrCoverageBackfill(session, globalCoverage);
                    if (backfillResult.Applied)
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
                            backfillResult.Applied,
                            executableLines: data[1],
                            coveredLines: data[2]);
                    }
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

            session.DrainIpcMessages(
                TimeSpan.FromMilliseconds(250),
                TimeSpan.FromMilliseconds(50),
                waitForFirstMessage: CoverageBackfillCapability.ShouldWaitForCoverageIpc(TestOptimization.Instance.Settings));
            var processedCoverletXmlReports = TryProcessCoverletCollectorXmlReports(session, recordCoverageResult: !session.HasCodeCoverageResults);
            if (CoverageBackfillDataStore.TryReadCoverageIpcFailure(out var ipcFailure))
            {
                if (!processedCoverletXmlReports && !session.HasCodeCoverageResults)
                {
                    Log.Warning("RunCiCommand: A selected coverage tool could not deliver its coverage result through IPC: {Reason}", ipcFailure);
                    TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageErrors();
                }
            }

            session.PublishCodeCoverage();

            session.Close(exitCode == 0 ? TestStatus.Pass : TestStatus.Fail);
        }

        /// <summary>
        /// Merges backend skipped-test coverage into Datadog internal global coverage when the session actually skipped tests by ITR.
        /// </summary>
        /// <param name="session">The test session that will publish the coverage result.</param>
        /// <param name="globalCoverage">The local global coverage model collected from executed tests.</param>
        /// <returns>Result of the backfill merge operation.</returns>
        internal static CoverageBackfillResult TryApplyItrCoverageBackfill(TestSession session, GlobalCoverageInfo globalCoverage)
        {
            if (!TryGetCoverageBackfillDataForSession(session, out var backfillData))
            {
                return new CoverageBackfillResult(applied: false, matchedFiles: 0, updatedFiles: 0);
            }

            var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfillData);
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
        /// Reads and optionally backfills an external XML coverage report for the supplied session.
        /// </summary>
        /// <param name="filePath">Coverage XML report path.</param>
        /// <param name="session">Test session that may publish the result.</param>
        /// <param name="result">Coverage result after optional backfill.</param>
        /// <returns>True if the XML report was parsed successfully.</returns>
        internal static bool TryProcessCoverageXml(string filePath, TestSession? session, out ExternalCoverageXmlResult result)
        {
            CoverageBackfillData? backfillData = null;
            var shouldBackfill = session is not null && TryGetCoverageBackfillDataForSession(session, out backfillData);
            return ExternalCoverageXmlBackfill.TryProcess(filePath, backfillData, shouldBackfill, out result);
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
            if (!CoverageBackfillCapability.IsCoverletCoverageCommand(commandLine))
            {
                return false;
            }

            var baseDirectory = GetAbsoluteWorkingDirectory(workingDirectory);
            if (TryGetCommandOptionValue(commandLine!, CoverletResultsDirectoryOptions, out var configuredResultsDirectory))
            {
                return TryResolveDirectoryPath(configuredResultsDirectory, baseDirectory, out resultsDirectory);
            }

            resultsDirectory = Path.Combine(baseDirectory, "TestResults");
            return true;
        }

        /// <summary>
        /// Rewrites Coverlet collector XML attachments after VSTest exits so report files stay accurate even when IPC is unavailable.
        /// </summary>
        /// <param name="session">Current test session.</param>
        /// <param name="recordCoverageResult">Whether processed XML results should be recorded for session coverage publication.</param>
        /// <returns>True when at least one Coverlet XML report was processed.</returns>
        private static bool TryProcessCoverletCollectorXmlReports(TestSession session, bool recordCoverageResult)
        {
            if (!TryGetCoverletCollectorResultsDirectory(GetCoverageBackfillCommandLine(session.Command), session.WorkingDirectory, out var resultsDirectory) ||
                !Directory.Exists(resultsDirectory))
            {
                return false;
            }

            var coverageFiles = Directory.EnumerateFiles(resultsDirectory, "coverage.cobertura.xml", SearchOption.AllDirectories)
                                         .Where(filePath => IsSessionCoverageAttachment(filePath, session.StartTime))
                                         .ToArray();
            if (coverageFiles.Length == 0)
            {
                return false;
            }

            var processedReports = 0;
            foreach (var coverageFile in coverageFiles)
            {
                if (!TryProcessCoverageXml(coverageFile, session, out var coverageResult))
                {
                    Log.Warning("RunCiCommand: Coverlet collector XML report could not be processed: {Path}", coverageFile);
                    TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageErrors();
                    continue;
                }

                processedReports++;
                if (recordCoverageResult)
                {
                    session.RecordCodeCoverage(
                        CodeCoverageReportSource.Coverlet,
                        coverageResult.Percentage,
                        coverageResult.Backfilled,
                        coverageResult.ExecutableLines,
                        coverageResult.CoveredLines,
                        coverageResult.Diagnostic);
                }
            }

            if (processedReports > 0)
            {
                Log.Information<int>("RunCiCommand: Coverlet collector XML reports processed. Count={Count}", processedReports);
            }

            return processedReports > 0;
        }

        /// <summary>
        /// Checks whether a Coverlet collector attachment was written during the current test session.
        /// </summary>
        /// <param name="filePath">Coverage attachment path.</param>
        /// <param name="sessionStartTime">Current test session start time.</param>
        /// <returns>True when the file timestamp is new enough to belong to this session.</returns>
        private static bool IsSessionCoverageAttachment(string filePath, DateTimeOffset sessionStartTime)
        {
            try
            {
                return File.GetLastWriteTimeUtc(filePath) >= sessionStartTime.UtcDateTime.AddSeconds(-1);
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
        /// Gets the normalized child test command used for coverage backfill internals without changing the public test session command tag.
        /// </summary>
        /// <param name="fallbackCommandLine">Public test session command used when no internal command was propagated.</param>
        /// <returns>Command line used to discover coverage tool outputs.</returns>
        private static string? GetCoverageBackfillCommandLine(string? fallbackCommandLine)
        {
            return EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand) ?? fallbackCommandLine;
        }

        /// <summary>
        /// Parses a command-line option value from a shell-like command string.
        /// </summary>
        /// <param name="commandLine">Command line to parse.</param>
        /// <param name="optionNames">Supported option spellings.</param>
        /// <param name="value">Option value when found.</param>
        /// <returns>True when the option was present and had a value.</returns>
        private static bool TryGetCommandOptionValue(string commandLine, string[] optionNames, out string value)
        {
            value = string.Empty;
            var arguments = SplitCommandLine(commandLine);
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                foreach (var optionName in optionNames)
                {
                    if (argument.Equals(optionName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (i + 1 < arguments.Count && !StringUtil.IsNullOrEmpty(arguments[i + 1]))
                        {
                            value = arguments[i + 1];
                            return true;
                        }

                        return false;
                    }

                    if (TryGetInlineOptionValue(argument, optionName, out value))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Reads an option value from single-token syntaxes such as <c>--ResultsDirectory:path</c> or <c>--results-directory=path</c>.
        /// </summary>
        /// <param name="argument">Command-line argument token.</param>
        /// <param name="optionName">Option name to match.</param>
        /// <param name="value">Inline option value when found.</param>
        /// <returns>True when the argument contains the option value.</returns>
        private static bool TryGetInlineOptionValue(string argument, string optionName, out string value)
        {
            value = string.Empty;
            if (!argument.StartsWith(optionName, StringComparison.OrdinalIgnoreCase) ||
                argument.Length <= optionName.Length)
            {
                return false;
            }

            var separator = argument[optionName.Length];
            if (separator is not ':' and not '=')
            {
                return false;
            }

            value = argument.Substring(optionName.Length + 1);
            return !StringUtil.IsNullOrEmpty(value);
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
        /// Splits a command line into arguments while preserving quoted spaces and removing quote delimiters.
        /// </summary>
        /// <param name="commandLine">Command line to split.</param>
        /// <returns>Parsed argument tokens.</returns>
        private static List<string> SplitCommandLine(string commandLine)
        {
            var arguments = new List<string>();
            var builder = new StringBuilder();
            var inQuotes = false;
            for (var i = 0; i < commandLine.Length; i++)
            {
                var character = commandLine[i];
                if (character == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (char.IsWhiteSpace(character) && !inQuotes)
                {
                    if (builder.Length > 0)
                    {
                        arguments.Add(builder.ToString());
                        builder.Clear();
                    }

                    continue;
                }

                builder.Append(character);
            }

            if (builder.Length > 0)
            {
                arguments.Add(builder.ToString());
            }

            return arguments;
        }

        /// <summary>
        /// Gets backend ITR coverage data when the current process has actually applied ITR skips.
        /// </summary>
        /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
        /// <returns>True when backfill data can be safely used by an in-process coverage tool adapter.</returns>
        internal static bool TryGetCoverageBackfillDataForCurrentProcess(out CoverageBackfillData backfillData)
        {
            backfillData = CoverageBackfillData.Missing;
            if (!CoverageBackfillDataStore.HasActualItrSkip())
            {
                return false;
            }

            var skippableFeature = TestOptimization.Instance.SkippableFeature;
            if (skippableFeature?.IsCoverageBackfillRequired() == true && skippableFeature.IsCoverageBackfillSafe())
            {
                backfillData = skippableFeature.GetCoverageBackfillData();
                return backfillData is { IsPresent: true, IsValid: true };
            }

            return CoverageBackfillDataStore.TryLoad(out backfillData);
        }

        private static bool HasActualItrSkips(TestSession session, ITestOptimizationSkippableFeature skippableFeature)
        {
            return skippableFeature.HasSkippedTestsByItr() ||
                   CoverageBackfillDataStore.HasActualItrSkip() ||
                   string.Equals(session.Tags.TestsSkipped, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetCoverageBackfillDataForSession(TestSession session, out CoverageBackfillData backfillData)
        {
            backfillData = CoverageBackfillData.Missing;
            var skippableFeature = TestOptimization.Instance.SkippableFeature;
            if (skippableFeature?.IsCoverageBackfillRequired() != true ||
                !HasActualItrSkips(session, skippableFeature))
            {
                return false;
            }

            if (skippableFeature.IsCoverageBackfillSafe())
            {
                backfillData = skippableFeature.GetCoverageBackfillData();
                if (backfillData is { IsPresent: true, IsValid: true })
                {
                    return true;
                }
            }

            return CoverageBackfillDataStore.TryLoad(out backfillData);
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
            if (string.IsNullOrEmpty(codeCoverageCollectorPath))
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
                var cleanItem = item.Replace(collectProperty, string.Empty)
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
                    var cleanItem = item.Replace(testAdapterPathProperty, string.Empty)
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
            if (string.IsNullOrEmpty(codeCoverageCollectorPath))
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
                        var argValue = arg.Replace(collectProperty, string.Empty)
                                          .Replace("\"", string.Empty);
                        disableCollectInjection = disableCollectInjection || argValue == datadogCoverageCollector;
                        continue;
                    }

                    if (!disableTestAdapterInjection && arg.StartsWith(testAdapterPathProperty, StringComparison.OrdinalIgnoreCase))
                    {
                        isTestAdapterPathIndex = i;
                        var argValue = arg.Replace(testAdapterPathProperty, string.Empty)
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
    }
}
