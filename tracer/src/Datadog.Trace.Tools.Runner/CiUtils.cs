// <copyright file="CiUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner;

internal static class CiUtils
{
    private const string RunnerOwnedCodeCoverageMarkerFileName = ".datadog-runner-owned-code-coverage";
    private const int MaxResponseFileExpansionDepth = 8;
    private const char QuotedResponseFileLiteralPrefix = '\x1F';

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CiUtils));

    private static readonly string[] DotnetSdkGlobalFlagOptions = ["-d", "--diagnostics", "--info", "--version"];

    private static readonly char[] CommandLineQuoteCharacters = [' ', '\t', '\r', '\n', '"'];
    private static readonly StringComparer PathComparer = FrameworkDescription.Instance.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

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

    private static readonly string[] RunScopedBackfillEnvironmentVariables =
    [
        Configuration.ConfigurationKeys.CIVisibility.TestOptimizationRunId,
        Configuration.ConfigurationKeys.CIVisibility.TestSessionCommand,
        Configuration.ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory,
        Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip,
        Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand,
        Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillPath,
        Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder
    ];

    public static async Task<InitResults> InitializeCiCommandsAsync(
        ApplicationContext applicationContext,
        InvocationContext context,
        CommonTracerSettings commonTracerSettings,
        Option<string>? apiKeyOption,
        string program,
        string[] args,
        bool includeRunScopedBackfillEnvironment,
        bool reducePathLength)
    {
        // Define the arguments
        var lstArguments = new List<string>(args.Length > 1 ? args.Skip(1) : []);
        var temporaryArgumentFiles = new List<string>();

        var explicitCodeCoveragePath = HasExplicitAdditionalEnvironmentVariable(context, commonTracerSettings, Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath);
        var clearInheritedCodeCoveragePath = includeRunScopedBackfillEnvironment &&
                                             !explicitCodeCoveragePath &&
                                             IsRunnerOwnedCodeCoveragePath(EnvironmentHelpers.GetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath));
        if (includeRunScopedBackfillEnvironment)
        {
            ClearInheritedRunScopedBackfillEnvironment(clearInheritedCodeCoveragePath);
        }

        // Get profiler environment variables
        if (!RunHelper.TryGetEnvironmentVariables(
                applicationContext,
                context,
                commonTracerSettings,
                new Utils.CIVisibilityOptions(TestOptimizationSettings.FromDefaultSources().InstallDatadogTraceInGac, true, reducePathLength),
                out var profilerEnvironmentVariables))
        {
            context.ExitCode = 1;
            return new InitResults(false, lstArguments, null, false, false, Task.CompletedTask);
        }

        if (includeRunScopedBackfillEnvironment)
        {
            ClearCurrentProcessRunScopedBackfillEnvironment(clearInheritedCodeCoveragePath);
            RemoveRunScopedBackfillEnvironmentVariables(profilerEnvironmentVariables);
            if (clearInheritedCodeCoveragePath)
            {
                profilerEnvironmentVariables.Remove(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath);
            }

            ClearCurrentProcessRunScopedPropagationEnvironmentVariables();
            RemoveRunScopedPropagationEnvironmentVariables(profilerEnvironmentVariables);
        }

        // Reload Test optimization instance and settings  (in case they were changed by the environment variables using the `--set-env` option)
        var testOptimization = new TestOptimization();
        var testOptimizationSettings = testOptimization.Settings;
        TestOptimization.Instance = testOptimization;

        // We force Test optimization mode on child process
        profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.Enabled] = "1";

        if (includeRunScopedBackfillEnvironment)
        {
            // These values are run-local state for child test processes, not persistent CI configuration.
            profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.TestOptimizationRunId] = testOptimization.RunId;
            profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder] = CoverageBackfillDataStore.GetNewRunFolder(testOptimization);
            profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip] = string.Empty;
            profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibilityItrCoverageBackfillPath] = string.Empty;
            if (clearInheritedCodeCoveragePath)
            {
                profilerEnvironmentVariables.Remove(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath);
            }
        }

        // We check the settings and merge with the command settings options
        var agentless = testOptimizationSettings.Agentless;
        var apiKey = testOptimizationSettings.ApiKey;

        var customApiKey = apiKeyOption?.GetValue(context);
        if (!string.IsNullOrEmpty(customApiKey))
        {
            agentless = true;
            apiKey = customApiKey;
            profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.AgentlessEnabled] = "1";
            profilerEnvironmentVariables[Configuration.ConfigurationKeys.ApiKey] = customApiKey;
        }

        var agentUrl = commonTracerSettings.AgentUrl.GetValue(context);

        // If the agentless feature flag is enabled, we check for ApiKey
        // If the agentless feature flag is disabled, we check if we have connection to the agent before running the process.
        AgentConfiguration? agentConfiguration = null;
        IDiscoveryService discoveryService = NullDiscoveryService.Instance;
        if (agentless)
        {
            Log.Debug("RunCiCommand: Agentless has been enabled. Checking API key");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Utils.WriteError("An API key is required in Agentless mode.");
                Log.Error("RunHelper: An API key is required in Agentless mode.");
                context.ExitCode = 1;
                return new InitResults(false, lstArguments, profilerEnvironmentVariables, false, false, Task.CompletedTask);
            }
        }
        else
        {
            Log.Debug("RunCiCommand: Agent-based mode has been enabled. Checking agent connection to {AgentUrl}.", agentUrl);
            (agentConfiguration, discoveryService) = await Utils.GetDiscoveryServiceAndCheckConnectionAsync(agentUrl).ConfigureAwait(false);
            if (agentConfiguration is null)
            {
                await discoveryService.DisposeAsync().ConfigureAwait(false);
                Log.Error("RunCiCommand: Agent configuration cannot be retrieved.");
                context.ExitCode = 1;
                return new InitResults(false, lstArguments, profilerEnvironmentVariables, false, false, Task.CompletedTask);
            }
        }

        var uploadRepositoryChangesTask = Task.CompletedTask;

        // Set Agentless configuration from the command line options
        testOptimizationSettings.SetAgentlessConfiguration(agentless, apiKey, testOptimizationSettings.AgentlessUrl);

        if (!string.IsNullOrEmpty(agentUrl))
        {
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.AgentUri, agentUrl);
        }

        // Initialize flags to enable code coverage and test skipping
        var internalCodeCoverageReportingEnabled = testOptimizationSettings.CodeCoverageEnabled == true;
        var codeCoverageEnabled = testOptimizationSettings.CodeCoverageEnabled == true || testOptimizationSettings.TestsSkippingEnabled == true;
        var testSkippingEnabled = testOptimizationSettings.TestsSkippingEnabled == true;
        var knownTestsEnabled = testOptimizationSettings.KnownTestsEnabled == true;
        var earlyFlakeDetectionEnabled = testOptimizationSettings.EarlyFlakeDetectionEnabled == true;
        var flakyRetryEnabled = testOptimizationSettings.FlakyRetryEnabled == true;
        var impactedTestsDetectionEnabled = testOptimizationSettings.ImpactedTestsDetectionEnabled == true;
        var testManagementEnabled = testOptimizationSettings.TestManagementEnabled == true;
        var dynamicInstrumentationEnabled = testOptimizationSettings.DynamicInstrumentationEnabled == true;

        var hasEvpProxy = !string.IsNullOrEmpty(agentConfiguration?.EventPlatformProxyEndpoint);
        if (agentless || hasEvpProxy)
        {
            // Initialize CI Visibility with the current settings
            Log.Debug("RunCiCommand: Initialize CI Visibility for the runner.");
            testOptimization.InitializeFromRunner(testOptimizationSettings, discoveryService, hasEvpProxy);

            // Upload git metadata by default (unless is disabled explicitly) or if ITR is enabled (required).
            Log.Debug("RunCiCommand: Uploading repository changes.");

            // Change the .git search folder to the CurrentDirectory or WorkingFolder
            var ciValues = testOptimization.CIValues;
            var client = TestOptimizationClient.Create(ciValues.WorkspacePath ?? Environment.CurrentDirectory, testOptimization);
            if (testOptimizationSettings.GitUploadEnabled != false || testOptimizationSettings.IntelligentTestRunnerEnabled)
            {
                // If we are in git upload only then we can defer the await until the child command exits.
                async Task UploadRepositoryChangesAsync()
                {
                    try
                    {
                        await client.UploadRepositoryChangesAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "RunCiCommand: Error uploading repository git metadata.");
                    }
                }

                uploadRepositoryChangesTask = Task.Run(UploadRepositoryChangesAsync);

                // Once the repository has been uploaded we switch off the git upload in children processes
                profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.GitUploadEnabled] = "0";
                EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.GitUploadEnabled, "0");
            }

            // We can activate all features here (Agentless or EVP proxy mode)
            if (!agentless)
            {
                // EVP proxy is enabled.
                // By setting the environment variables we avoid the usage of the DiscoveryService in each child process
                // to ask for EVP proxy support.
                var evpProxyMode = testOptimization.TracerManagement?.EventPlatformProxySupportFromEndpointUrl(agentConfiguration?.EventPlatformProxyEndpoint).ToString();
                profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.ForceAgentsEvpProxy] = evpProxyMode;
                EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ForceAgentsEvpProxy, evpProxyMode);
                Log.Debug("RunCiCommand: EVP proxy was detected: {Mode}", evpProxyMode);
            }

            // If we have an api key, and the code coverage or the tests skippable environment variables
            // are not set when the intelligent test runner is enabled, we query the settings api to check if it should enable coverage or not.
            if (!testOptimizationSettings.IntelligentTestRunnerEnabled)
            {
                Log.Debug("RunCiCommand: Intelligent test runner is disabled, call to configuration api skipped.");
            }

            // If we still don't know if we have to enable code coverage or test skipping, then let's request the configuration API
            if (testOptimizationSettings.IntelligentTestRunnerEnabled
             && (testOptimizationSettings.CodeCoverageEnabled == null ||
                 testOptimizationSettings.TestsSkippingEnabled == null ||
                 testOptimizationSettings.KnownTestsEnabled == null ||
                 testOptimizationSettings.EarlyFlakeDetectionEnabled == null ||
                 testOptimizationSettings.FlakyRetryEnabled == null ||
                 testOptimizationSettings.DynamicInstrumentationEnabled == null ||
                 testOptimizationSettings.ImpactedTestsDetectionEnabled == null ||
                 testOptimizationSettings.TestManagementEnabled == null))
            {
                try
                {
                    testOptimization.Log.Debug("RunCiCommand: Calling configuration api...");

                    // we skip the framework info because we are interested in the target projects info not the runner one.
                    var itrSettings = await client.GetSettingsAsync(skipFrameworkInfo: true).ConfigureAwait(false);

                    // we check if the backend require the git metadata first
                    if (itrSettings.RequireGit == true)
                    {
                        Log.Debug("RunCiCommand: require git received, awaiting for the git repository upload.");
                        await uploadRepositoryChangesTask.ConfigureAwait(false);

                        Log.Debug("RunCiCommand: calling the configuration api again.");
                        itrSettings = await client.GetSettingsAsync(skipFrameworkInfo: true).ConfigureAwait(false);
                    }

                    internalCodeCoverageReportingEnabled = internalCodeCoverageReportingEnabled || itrSettings.CodeCoverage == true;
                    codeCoverageEnabled = codeCoverageEnabled || itrSettings.CodeCoverage == true || itrSettings.TestsSkipping == true;
                    testSkippingEnabled = itrSettings.TestsSkipping == true;
                    knownTestsEnabled = knownTestsEnabled || itrSettings.KnownTestsEnabled == true;
                    earlyFlakeDetectionEnabled = earlyFlakeDetectionEnabled || itrSettings.EarlyFlakeDetection.Enabled == true;
                    flakyRetryEnabled = flakyRetryEnabled || itrSettings.FlakyTestRetries == true;
                    impactedTestsDetectionEnabled = impactedTestsDetectionEnabled || itrSettings.ImpactedTestsEnabled == true;
                    testManagementEnabled = testManagementEnabled || itrSettings.TestManagement.Enabled == true;
                    dynamicInstrumentationEnabled = dynamicInstrumentationEnabled || itrSettings.DynamicInstrumentationEnabled == true;
                }
                catch (Exception ex)
                {
                    testOptimization.Log.Warning(ex, "Error getting ITR settings from configuration api");
                }
            }
        }
        else
        {
            // Didn't use the discovery service, so we need to dispose it
            await discoveryService.DisposeAsync().ConfigureAwait(false);
        }

        Log.Debug("RunCiCommand: CodeCoverageEnabled = {Value}", codeCoverageEnabled);
        Log.Debug("RunCiCommand: TestSkippingEnabled = {Value}", testSkippingEnabled);
        Log.Debug("RunCiCommand: KnownTestsEnabled = {Value}", knownTestsEnabled);
        Log.Debug("RunCiCommand: EarlyFlakeDetectionEnabled = {Value}", earlyFlakeDetectionEnabled);
        Log.Debug("RunCiCommand: FlakyRetryEnabled = {Value}", flakyRetryEnabled);
        Log.Debug("RunCiCommand: DynamicInstrumentationEnabled = {Value}", dynamicInstrumentationEnabled);
        Log.Debug("RunCiCommand: ImpactedTestsDetectionEnabled = {Value}", impactedTestsDetectionEnabled);
        Log.Debug("RunCiCommand: TestManagementEnabled = {Value}", testManagementEnabled);
        testOptimizationSettings.SetCodeCoverageEnabled(codeCoverageEnabled);
        testOptimizationSettings.SetKnownTestsEnabled(knownTestsEnabled);
        testOptimizationSettings.SetEarlyFlakeDetectionEnabled(earlyFlakeDetectionEnabled);
        testOptimizationSettings.SetFlakyRetryEnabled(flakyRetryEnabled);
        testOptimizationSettings.SetDynamicInstrumentationEnabled(dynamicInstrumentationEnabled);
        testOptimizationSettings.SetImpactedTestsEnabled(impactedTestsDetectionEnabled);
        testOptimizationSettings.SetTestManagementEnabled(testManagementEnabled);
        profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.CodeCoverage] = codeCoverageEnabled ? "1" : "0";
        profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.KnownTestsEnabled] = knownTestsEnabled ? "1" : "0";
        profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled] = earlyFlakeDetectionEnabled ? "1" : "0";
        profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.FlakyRetryEnabled] = flakyRetryEnabled ? "1" : "0";
        profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.DynamicInstrumentationEnabled] = dynamicInstrumentationEnabled ? "1" : "0";
        profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.ImpactedTestsDetectionEnabled] = impactedTestsDetectionEnabled ? "1" : "0";
        profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.TestManagementEnabled] = testManagementEnabled ? "1" : "0";

        if (!testSkippingEnabled)
        {
            // If test skipping is disabled we set this to the child process so we avoid to query the settings api again.
            // If is not disabled we need to query the backend again in the child process with more runtime info.
            testOptimizationSettings.SetTestsSkippingEnabled(testSkippingEnabled);
            profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.TestsSkippingEnabled] = "0";
        }

        // If dynamic instrumentation is enabled, we set the environment variable
        if (dynamicInstrumentationEnabled)
        {
            profilerEnvironmentVariables[Configuration.ConfigurationKeys.Debugger.ExceptionReplayEnabled] = "1";
            profilerEnvironmentVariables[Configuration.ConfigurationKeys.Debugger.RateLimitSeconds] = "0";
            profilerEnvironmentVariables[Configuration.ConfigurationKeys.Debugger.UploadFlushInterval] = "1000";

            if (agentless)
            {
                profilerEnvironmentVariables[Configuration.ConfigurationKeys.Debugger.ExceptionReplayAgentlessEnabled] = "1";
            }
        }

        // Let's set the code coverage datacollector if the code coverage is enabled
        if (codeCoverageEnabled)
        {
            profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath] = AppContext.BaseDirectory;

            var programFileName = GetExecutableFileName(program);
            var isDotnetCommand = string.Equals(programFileName, "dotnet", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(programFileName, "dotnet.exe", StringComparison.OrdinalIgnoreCase);
            var isVsTestConsoleCommand = IsVstestConsoleFileName(programFileName);

            Log.Debug("Program = {Program} | IsDotnetCommand? {IsDotnetCommand} | IsVsTestConsoleCommand? {IsVsTestConsoleCommand}", program, isDotnetCommand, isVsTestConsoleCommand);

            // Check if we are running dotnet process
            if (isDotnetCommand || isVsTestConsoleCommand)
            {
                // Try to find the test command type: `dotnet test` or `dotnet vstest`
                var isDotnetTestCommand = false;
                var isDotnetVsTestCommand = false;
                var materializeExpandedArgumentsInResponseFile = false;
                var expandedArguments = lstArguments;
                if (isDotnetCommand || isVsTestConsoleCommand)
                {
                    expandedArguments = ExpandResponseFileArguments(lstArguments, Environment.CurrentDirectory, depth: 0, visitedFiles: null);
                }

                if (!isVsTestConsoleCommand && TryGetDotnetSdkCommand(expandedArguments, out var dotnetCommand))
                {
                    isDotnetTestCommand = string.Equals(dotnetCommand, "test", StringComparison.OrdinalIgnoreCase);
                    isDotnetVsTestCommand = string.Equals(dotnetCommand, "vstest", StringComparison.OrdinalIgnoreCase) ||
                                            IsVstestConsoleFileName(GetExecutableFileName(dotnetCommand));
                }

                Log.Debug("IsDotnetTestCommand? {IsDotnetTestCommand} | IsDotnetVsTestCommand? {IsDotnetVsTestCommand}", isDotnetTestCommand, isDotnetVsTestCommand);

                // Add the Datadog coverage collector
                var baseDirectory = AppContext.BaseDirectory;

                var usesTestingPlatformCoverage = isDotnetTestCommand &&
                                                   CoverageBackfillCapability.IsTestingPlatformCoverageCommand(GetCommandLine(program, expandedArguments), Environment.CurrentDirectory);

                var hasExpandedResponseFileArguments = !ReferenceEquals(expandedArguments, lstArguments);
                if (((isDotnetTestCommand && !usesTestingPlatformCoverage) || isDotnetVsTestCommand || isVsTestConsoleCommand) &&
                    hasExpandedResponseFileArguments &&
                    IndexOfDoubleDash(expandedArguments) >= 0)
                {
                    lstArguments = new List<string>(expandedArguments);
                    materializeExpandedArgumentsInResponseFile = true;
                }

                var doubleDashIndex = IndexOfDoubleDash(lstArguments);

                if (isDotnetTestCommand && !usesTestingPlatformCoverage)
                {
                    var alreadyCollectsDatadogCoverage = HasDatadogCoverageCollector(program, lstArguments);
                    if (doubleDashIndex == -1)
                    {
                        lstArguments.Add("--test-adapter-path");
                        lstArguments.Add(baseDirectory);
                        if (!alreadyCollectsDatadogCoverage)
                        {
                            lstArguments.Add("--collect");
                            lstArguments.Add("DatadogCoverage");
                        }
                    }
                    else
                    {
                        lstArguments.Insert(doubleDashIndex, "--test-adapter-path");
                        lstArguments.Insert(doubleDashIndex + 1, baseDirectory);
                        if (!alreadyCollectsDatadogCoverage)
                        {
                            lstArguments.Insert(doubleDashIndex + 2, "--collect");
                            lstArguments.Insert(doubleDashIndex + 3, "DatadogCoverage");
                        }
                    }

                    Log.Debug("DatadogCoverage data collector added as a command argument");
                }
                else if (isDotnetTestCommand)
                {
                    Log.Debug("DatadogCoverage data collector was not added because Microsoft Testing Platform coverage is already selected.");
                }
                else if (isVsTestConsoleCommand || isDotnetVsTestCommand)
                {
                    var alreadyCollectsDatadogCoverage = HasDatadogCoverageCollector(program, lstArguments);
                    if (doubleDashIndex == -1)
                    {
                        lstArguments.Add("/TestAdapterPath:" + baseDirectory);
                        if (!alreadyCollectsDatadogCoverage)
                        {
                            lstArguments.Add("/Collect:DatadogCoverage");
                        }
                    }
                    else
                    {
                        lstArguments.Insert(doubleDashIndex, "/TestAdapterPath:" + baseDirectory);
                        if (!alreadyCollectsDatadogCoverage)
                        {
                            lstArguments.Insert(doubleDashIndex + 1, "/Collect:DatadogCoverage");
                        }
                    }

                    Log.Debug("DatadogCoverage data collector added as a command argument");
                }
                else
                {
                    Log.Warning("RunCiCommand: Code coverage is enabled but the command is not a 'dotnet test' nor 'dotnet vstest' nor 'vstest' nor 'vstest.console' command. Code coverage will not be collected.");
                }

                if (materializeExpandedArgumentsInResponseFile)
                {
                    if (TryCreateRunnerOwnedResponseFile(lstArguments, temporaryArgumentFiles, out var responseFileArguments))
                    {
                        lstArguments = responseFileArguments;
                    }
                    else
                    {
                        Log.Warning("RunCiCommand: Could not create a response file for expanded test arguments; falling back to expanded arguments.");
                    }
                }

                // Store Datadog global coverage when it is complete by construction, or when explicit reporting can be corrected by ITR backfill.
                var hasUserCodeCoveragePath = explicitCodeCoveragePath ||
                                              (!StringUtil.IsNullOrWhiteSpace(testOptimizationSettings.CodeCoveragePath) &&
                                               !IsRunnerOwnedCodeCoveragePath(testOptimizationSettings.CodeCoveragePath));
                if ((!testSkippingEnabled || internalCodeCoverageReportingEnabled) && !hasUserCodeCoveragePath)
                {
                    var outputFolders = new[] { Environment.CurrentDirectory, Path.GetTempPath(), };
                    foreach (var folder in outputFolders)
                    {
                        var outputPath = Path.Combine(folder, $"datadog-coverage-{DateTime.Now:yyyy-MM-dd_HH_mm_ss}-{testOptimization.RunId}");
                        if (!Directory.Exists(outputPath))
                        {
                            try
                            {
                                Directory.CreateDirectory(outputPath);
                                File.WriteAllText(Path.Combine(outputPath, RunnerOwnedCodeCoverageMarkerFileName), string.Empty);
                                profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath] = outputPath;
                                EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, outputPath);
                                break;
                            }
                            catch (Exception ex)
                            {
                                Utils.WriteError("Error creating folder for the global code coverage files:");
                                AnsiConsole.WriteException(ex);
                            }
                        }
                    }
                }
            }
            else
            {
                Log.Warning("RunCiCommand: Code coverage is enabled but the command is not a 'dotnet' nor 'vstest' nor 'vstest.console' command. Code coverage will not be collected.");
            }
        }

        return new InitResults(true, lstArguments, profilerEnvironmentVariables, testSkippingEnabled, codeCoverageEnabled, uploadRepositoryChangesTask, temporaryArgumentFiles);
    }

    private static bool HasExplicitAdditionalEnvironmentVariable(InvocationContext context, CommonTracerSettings commonTracerSettings, string key)
    {
        if (commonTracerSettings is not RunSettings runSettings)
        {
            return false;
        }

        var additionalEnvironmentVariables = context.ParseResult.GetValueForOption(runSettings.AdditionalEnvironmentVariables);
        if (additionalEnvironmentVariables is null)
        {
            return false;
        }

        foreach (var environmentVariable in additionalEnvironmentVariables)
        {
            var separatorIndex = environmentVariable.IndexOf('=');
            if (separatorIndex > 0 &&
                string.Equals(environmentVariable.Substring(0, separatorIndex), key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRunnerOwnedCodeCoveragePath(string? path)
    {
        if (StringUtil.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var directoryName = Path.GetFileName(path!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return IsRunnerOwnedCodeCoverageDirectoryName(directoryName) &&
                   File.Exists(Path.Combine(path!, RunnerOwnedCodeCoverageMarkerFileName));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsRunnerOwnedCodeCoverageDirectoryName(string directoryName)
    {
        const string prefix = "datadog-coverage-";
        const int timestampLength = 19;
        if (directoryName.Length < prefix.Length + timestampLength ||
            !directoryName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = directoryName.Substring(prefix.Length);
        if (!IsRunnerOwnedCodeCoverageTimestamp(suffix.Substring(0, timestampLength)))
        {
            return false;
        }

        return suffix.Length == timestampLength ||
               (suffix.Length > timestampLength + 1 && suffix[timestampLength] == '-');
    }

    private static bool IsRunnerOwnedCodeCoverageTimestamp(string timestamp)
    {
        return timestamp.Length == 19 &&
               timestamp[4] == '-' &&
               timestamp[7] == '-' &&
               timestamp[10] == '_' &&
               timestamp[13] == '_' &&
               timestamp[16] == '_' &&
               IsAsciiDigit(timestamp, 0) &&
               IsAsciiDigit(timestamp, 1) &&
               IsAsciiDigit(timestamp, 2) &&
               IsAsciiDigit(timestamp, 3) &&
               IsAsciiDigit(timestamp, 5) &&
               IsAsciiDigit(timestamp, 6) &&
               IsAsciiDigit(timestamp, 8) &&
               IsAsciiDigit(timestamp, 9) &&
               IsAsciiDigit(timestamp, 11) &&
               IsAsciiDigit(timestamp, 12) &&
               IsAsciiDigit(timestamp, 14) &&
               IsAsciiDigit(timestamp, 15) &&
               IsAsciiDigit(timestamp, 17) &&
               IsAsciiDigit(timestamp, 18);
    }

    private static bool IsAsciiDigit(string value, int index)
    {
        return value[index] >= '0' && value[index] <= '9';
    }

    private static void RemoveRunScopedPropagationEnvironmentVariables(Dictionary<string, string> environmentVariables)
    {
        foreach (var key in RunScopedPropagationEnvironmentVariables)
        {
            environmentVariables.Remove(key);
        }
    }

    private static void RemoveRunScopedBackfillEnvironmentVariables(Dictionary<string, string> environmentVariables)
    {
        foreach (var key in RunScopedBackfillEnvironmentVariables)
        {
            environmentVariables.Remove(key);
        }
    }

    private static bool HasDatadogCoverageCollector(string program, IReadOnlyCollection<string> arguments)
    {
        return CoverageBackfillCommandLine.Parse(GetCommandLine(program, arguments), Environment.CurrentDirectory)
                                          .UsesDatadogCoverageCollector();
    }

    private static string GetCommandLine(string program, IReadOnlyCollection<string> arguments)
    {
        var builder = new StringBuilder();
        builder.Append(QuoteCommandLineArgument(program));
        foreach (var argument in arguments)
        {
            builder.Append(' ');
            builder.Append(QuoteCommandLineArgument(argument));
        }

        return builder.ToString();
    }

    private static bool TryCreateRunnerOwnedResponseFile(IReadOnlyCollection<string> arguments, ICollection<string> temporaryArgumentFiles, out List<string> responseFileArguments)
    {
        responseFileArguments = [];
        try
        {
            var responseFilePath = Path.Combine(Path.GetTempPath(), $"dd-trace-ci-run-{Guid.NewGuid():N}.rsp");
            File.WriteAllLines(responseFilePath, arguments.Select(QuoteCommandLineArgument), Encoding.UTF8);
            temporaryArgumentFiles.Add(responseFilePath);
            responseFileArguments = ["@" + responseFilePath];
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "RunCiCommand: Error writing runner-owned response file.");
            return false;
        }
    }

    private static string QuoteCommandLineArgument(string argument)
    {
        if (StringUtil.IsNullOrEmpty(argument))
        {
            return "\"\"";
        }

        if (argument.IndexOfAny(CommandLineQuoteCharacters) < 0 &&
            !StartsWithResponseFileSpecialCharacter(argument))
        {
            return argument;
        }

        var builder = new StringBuilder(argument.Length + 2);
        builder.Append('"');
        var backslashCount = 0;
        foreach (var currentChar in argument)
        {
            if (currentChar == '\\')
            {
                backslashCount++;
                continue;
            }

            if (currentChar == '"')
            {
                builder.Append('\\', (backslashCount * 2) + 1);
                builder.Append('"');
                backslashCount = 0;
                continue;
            }

            if (backslashCount > 0)
            {
                builder.Append('\\', backslashCount);
                backslashCount = 0;
            }

            builder.Append(currentChar);
        }

        if (backslashCount > 0)
        {
            builder.Append('\\', backslashCount * 2);
        }

        builder.Append('"');
        return builder.ToString();
    }

    private static bool StartsWithResponseFileSpecialCharacter(string argument)
    {
        // Runner-owned response files write one argument per line; dotnet treats leading
        // '#' as a comment and leading '@' as another response-file reference.
        return argument[0] == '#' || argument[0] == '@';
    }

    private static string GetExecutableFileName(string program)
    {
        var normalizedProgram = program.Trim('"').Replace('\\', '/');
        var separatorIndex = normalizedProgram.LastIndexOf('/');
        return separatorIndex >= 0 ? normalizedProgram.Substring(separatorIndex + 1) : normalizedProgram;
    }

    private static bool IsVstestConsoleFileName(string fileName)
    {
        fileName = GetExecutableFileName(fileName);
        if (fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName.Substring(0, fileName.Length - 4);
        }

        return string.Equals(fileName, "vstest", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "VSTest.Console", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "VSTest.Console.Arm64", StringComparison.OrdinalIgnoreCase);
    }

    private static int IndexOfDoubleDash(IReadOnlyList<string> arguments)
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i] == "--")
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryGetDotnetSdkCommand(IReadOnlyList<string> arguments, out string command)
    {
        command = string.Empty;
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i].Trim('"');
            if (IsDotnetSdkGlobalFlagOption(argument))
            {
                continue;
            }

            if (argument.Length > 1 && argument[0] == '-')
            {
                return false;
            }

            command = argument;
            return command.Length > 0;
        }

        return false;
    }

    private static List<string> ExpandResponseFileArguments(List<string> arguments, string? baseDirectory, int depth, HashSet<string>? visitedFiles)
    {
        if (arguments.Count == 0 || depth >= MaxResponseFileExpansionDepth)
        {
            return arguments;
        }

        List<string>? expandedArguments = null;
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            var responseFilePath = TryResolveResponseFilePath(argument, baseDirectory);
            if (responseFilePath is null)
            {
                expandedArguments?.Add(StripQuotedResponseFileLiteralPrefix(argument));
                continue;
            }

            visitedFiles ??= new HashSet<string>(PathComparer);
            if (!visitedFiles.Add(responseFilePath))
            {
                expandedArguments?.Add(StripQuotedResponseFileLiteralPrefix(argument));
                continue;
            }

            try
            {
                var responseFileArguments = ExpandResponseFileArguments(
                    SplitResponseFileCommandLine(File.ReadAllText(responseFilePath)),
                    baseDirectory,
                    depth + 1,
                    visitedFiles);

                expandedArguments ??= arguments.GetRange(0, i);
                expandedArguments.AddRange(responseFileArguments.Select(StripQuotedResponseFileLiteralPrefix));
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "RunCiCommand: Error reading response file for command argument expansion.");
                expandedArguments?.Add(StripQuotedResponseFileLiteralPrefix(argument));
            }
            finally
            {
                visitedFiles.Remove(responseFilePath);
            }
        }

        return StripQuotedResponseFileLiteralPrefixes(expandedArguments ?? arguments);
    }

    private static List<string> SplitResponseFileCommandLine(string commandLine)
    {
        var arguments = new List<string>();
        using var reader = new StringReader(commandLine);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var argument = line.Trim();
            if (StringUtil.IsNullOrEmpty(argument) ||
                argument.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            arguments.AddRange(SplitCommandLine(argument, preserveQuotedResponseFileLiterals: true));
        }

        return arguments;
    }

    private static List<string> SplitCommandLine(string? commandLine, bool preserveQuotedResponseFileLiterals = false)
    {
        var arguments = new List<string>();
        if (commandLine is null || StringUtil.IsNullOrWhiteSpace(commandLine))
        {
            return arguments;
        }

        var currentArgument = new StringBuilder(commandLine.Length);
        var inQuotes = false;
        var argumentStartedWithQuote = false;
        var quotedResponseFileLiteral = false;
        var backslashCount = 0;

        for (var i = 0; i < commandLine.Length; i++)
        {
            var currentChar = commandLine[i];
            if (currentChar == '\\')
            {
                backslashCount++;
                continue;
            }

            if (currentChar == '"')
            {
                currentArgument.Append('\\', backslashCount / 2);
                if ((backslashCount & 1) == 0)
                {
                    if (!inQuotes)
                    {
                        argumentStartedWithQuote = currentArgument.Length == 0;
                    }
                    else if (preserveQuotedResponseFileLiterals &&
                             argumentStartedWithQuote &&
                             currentArgument.Length > 0 &&
                             currentArgument[0] == '@')
                    {
                        quotedResponseFileLiteral = true;
                    }

                    inQuotes = !inQuotes;
                }
                else
                {
                    currentArgument.Append(currentChar);
                }

                backslashCount = 0;
                continue;
            }

            if (backslashCount > 0)
            {
                currentArgument.Append('\\', backslashCount);
                backslashCount = 0;
            }

            if (!inQuotes && char.IsWhiteSpace(currentChar))
            {
                if (currentArgument.Length > 0)
                {
                    AddCommandLineArgument(arguments, currentArgument.ToString(), quotedResponseFileLiteral);
                    currentArgument.Clear();
                }

                argumentStartedWithQuote = false;
                quotedResponseFileLiteral = false;
                continue;
            }

            currentArgument.Append(currentChar);
        }

        if (backslashCount > 0)
        {
            currentArgument.Append('\\', backslashCount);
        }

        if (currentArgument.Length > 0)
        {
            AddCommandLineArgument(arguments, currentArgument.ToString(), quotedResponseFileLiteral);
        }

        return arguments;
    }

    private static void AddCommandLineArgument(List<string> arguments, string argument, bool quotedResponseFileLiteral)
    {
        arguments.Add(quotedResponseFileLiteral ? QuotedResponseFileLiteralPrefix + argument : argument);
    }

    private static string? TryResolveResponseFilePath(string argument, string? baseDirectory)
    {
        if (!TryGetResponseFileReference(argument, out var responseFilePath))
        {
            return null;
        }

        if (StringUtil.IsNullOrWhiteSpace(responseFilePath))
        {
            return null;
        }

        try
        {
            if (!Path.IsPathRooted(responseFilePath) && !StringUtil.IsNullOrWhiteSpace(baseDirectory))
            {
                responseFilePath = Path.Combine(baseDirectory, responseFilePath);
            }

            responseFilePath = Path.GetFullPath(responseFilePath);
            return File.Exists(responseFilePath) ? responseFilePath : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool TryGetResponseFileReference(string argument, out string responseFilePath)
    {
        responseFilePath = string.Empty;
        var responseFileReference = argument.Trim();
        if (IsQuotedResponseFileLiteral(responseFileReference))
        {
            return false;
        }

        responseFileReference = StripOuterResponseFileQuotes(responseFileReference);
        if (responseFileReference.Length <= 1 || responseFileReference[0] != '@')
        {
            return false;
        }

        responseFilePath = responseFileReference.Substring(1);
        return true;
    }

    private static string StripOuterResponseFileQuotes(string value)
    {
        if (value.Length >= 2 &&
            value[0] == '"' &&
            value[value.Length - 1] == '"')
        {
            return value.Substring(1, value.Length - 2);
        }

        return value;
    }

    private static bool IsQuotedResponseFileLiteral(string value)
        => value.Length > 0 && value[0] == QuotedResponseFileLiteralPrefix;

    private static string StripQuotedResponseFileLiteralPrefix(string value)
        => IsQuotedResponseFileLiteral(value) ? value.Substring(1) : value;

    private static List<string> StripQuotedResponseFileLiteralPrefixes(List<string> arguments)
    {
        List<string>? strippedArguments = null;
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            var strippedArgument = StripQuotedResponseFileLiteralPrefix(argument);
            if (!ReferenceEquals(strippedArgument, argument) && strippedArguments is null)
            {
                strippedArguments = arguments.GetRange(0, i);
            }

            strippedArguments?.Add(strippedArgument);
        }

        return strippedArguments ?? arguments;
    }

    private static bool IsDotnetSdkGlobalFlagOption(string argument)
    {
        foreach (var option in DotnetSdkGlobalFlagOptions)
        {
            if (argument.Equals(option, StringComparison.OrdinalIgnoreCase) ||
                (argument.Length > option.Length &&
                 argument.StartsWith(option, StringComparison.OrdinalIgnoreCase) &&
                 argument[option.Length] is '=' or ':'))
            {
                return true;
            }
        }

        return false;
    }

    private static void ClearInheritedRunScopedBackfillEnvironment(bool clearCodeCoveragePath)
    {
        ClearCurrentProcessRunScopedBackfillEnvironment(clearCodeCoveragePath);
        ClearCurrentProcessRunScopedPropagationEnvironmentVariables();
    }

    private static void ClearCurrentProcessRunScopedBackfillEnvironment(bool clearCodeCoveragePath)
    {
        foreach (var key in RunScopedBackfillEnvironmentVariables)
        {
            EnvironmentHelpers.SetEnvironmentVariable(key, null);
        }

        if (clearCodeCoveragePath)
        {
            EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath, null);
        }
    }

    private static void ClearCurrentProcessRunScopedPropagationEnvironmentVariables()
    {
        foreach (var key in RunScopedPropagationEnvironmentVariables)
        {
            EnvironmentHelpers.SetEnvironmentVariable(key, null);
        }
    }

    public class InitResults
    {
        private readonly Task _uploadRepositoryChangesTask;

        public InitResults(bool success, ICollection<string> arguments, Dictionary<string, string>? profilerEnvironmentVariables, bool testSkippingEnabled, bool codeCoverageEnabled, Task uploadRepositoryChangesTask, ICollection<string>? temporaryArgumentFiles = null)
        {
            Success = success;
            Arguments = arguments;
            ProfilerEnvironmentVariables = profilerEnvironmentVariables;
            TestSkippingEnabled = testSkippingEnabled;
            CodeCoverageEnabled = codeCoverageEnabled;
            TemporaryArgumentFiles = temporaryArgumentFiles ?? [];
            _uploadRepositoryChangesTask = uploadRepositoryChangesTask;
        }

        public bool Success { get; }

        public ICollection<string> Arguments { get; }

        public Dictionary<string, string>? ProfilerEnvironmentVariables { get; }

        public bool TestSkippingEnabled { get; }

        public bool CodeCoverageEnabled { get; }

        public ICollection<string> TemporaryArgumentFiles { get; }

        public async Task UploadRepositoryChangesTask()
        {
            Log.Debug("RunCiCommand: Awaiting for the Git repository upload.");
            await _uploadRepositoryChangesTask.ConfigureAwait(false);
        }

        public void CleanupTemporaryArgumentFiles()
        {
            foreach (var filePath in TemporaryArgumentFiles)
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "RunCiCommand: Error deleting runner-owned response file.");
                }
            }
        }
    }
}
