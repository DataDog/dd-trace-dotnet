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
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner;

internal static class CiUtils
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CiUtils));

    public static async Task<InitResults> InitializeCiCommandsAsync(
        ApplicationContext applicationContext,
        InvocationContext context,
        CommonTracerSettings commonTracerSettings,
        Option<string>? apiKeyOption,
        string program,
        string[] args,
        bool reducePathLength)
    {
        // Define the arguments
        var lstArguments = new List<string>(args.Length > 1 ? args.Skip(1) : []);

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

        // Reload Test optimization instance and settings  (in case they were changed by the environment variables using the `--set-env` option)
        var testOptimization = new TestOptimization();
        var testOptimizationSettings = testOptimization.Settings;
        TestOptimization.Instance = testOptimization;

        // We force Test optimization mode on child process
        profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.Enabled] = "1";

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
            ciValues.GitSearchFolder = Environment.CurrentDirectory;
            if (string.IsNullOrEmpty(ciValues.WorkspacePath))
            {
                // In case we cannot get the WorkspacePath we fallback to the default configuration.
                ciValues.GitSearchFolder = null;
            }

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
        }

        // Let's set the code coverage datacollector if the code coverage is enabled
        if (codeCoverageEnabled)
        {
            profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath] = AppContext.BaseDirectory;

            var isDotnetCommand = string.Equals(program, "dotnet", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(program, "dotnet.exe", StringComparison.OrdinalIgnoreCase) ||
                                  program.EndsWith("/dotnet.exe", StringComparison.OrdinalIgnoreCase) ||
                                  program.EndsWith("\\dotnet.exe", StringComparison.OrdinalIgnoreCase);
            var isVsTestConsoleCommand = string.Equals(program, "VSTest.Console", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(program, "VSTest.Console.exe", StringComparison.OrdinalIgnoreCase) ||
                                         program.EndsWith("/VSTest.Console.exe", StringComparison.OrdinalIgnoreCase) ||
                                         program.EndsWith("\\VSTest.Console.exe", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(program, "VSTest.Console.Arm64", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(program, "VSTest.Console.Arm64.exe", StringComparison.OrdinalIgnoreCase) ||
                                         program.EndsWith("/VSTest.Console.Arm64.exe", StringComparison.OrdinalIgnoreCase) ||
                                         program.EndsWith("\\VSTest.Console.Arm64.exe", StringComparison.OrdinalIgnoreCase);

            Log.Debug("Program = {Program} | IsDotnetCommand? {IsDotnetCommand} | IsVsTestConsoleCommand? {IsVsTestConsoleCommand}", program, isDotnetCommand, isVsTestConsoleCommand);

            // Check if we are running dotnet process
            if (isDotnetCommand || isVsTestConsoleCommand)
            {
                // Try to find the test command type: `dotnet test` or `dotnet vstest`
                var isDotnetTestCommand = false;
                var isDotnetVsTestCommand = false;
                if (!isVsTestConsoleCommand)
                {
                    foreach (var arg in args.Skip(1))
                    {
                        isDotnetTestCommand |= string.Equals(arg, "test", StringComparison.OrdinalIgnoreCase);
                        isDotnetVsTestCommand |= string.Equals(arg, "vstest", StringComparison.OrdinalIgnoreCase);

                        if (isDotnetTestCommand || isDotnetVsTestCommand)
                        {
                            break;
                        }
                    }
                }

                Log.Debug("IsDotnetTestCommand? {IsDotnetTestCommand} | IsDotnetVsTestCommand? {IsDotnetVsTestCommand}", isDotnetTestCommand, isDotnetVsTestCommand);

                // Add the Datadog coverage collector
                var baseDirectory = AppContext.BaseDirectory;

                var doubleDashIndex = -1;
                for (var i = 0; i < lstArguments.Count; i++)
                {
                    if (lstArguments[i] == "--")
                    {
                        doubleDashIndex = i;
                        break;
                    }
                }

                if (isDotnetTestCommand)
                {
                    if (doubleDashIndex == -1)
                    {
                        lstArguments.Add("--test-adapter-path");
                        lstArguments.Add(baseDirectory);
                        lstArguments.Add("--collect");
                        lstArguments.Add("DatadogCoverage");
                    }
                    else
                    {
                        lstArguments.Insert(doubleDashIndex, "--test-adapter-path");
                        lstArguments.Insert(doubleDashIndex + 1, baseDirectory);
                        lstArguments.Insert(doubleDashIndex + 2, "--collect");
                        lstArguments.Insert(doubleDashIndex + 3, "DatadogCoverage");
                    }

                    Log.Debug("DatadogCoverage data collector added as a command argument");
                }
                else if (isVsTestConsoleCommand || isDotnetVsTestCommand)
                {
                    if (doubleDashIndex == -1)
                    {
                        lstArguments.Add("/TestAdapterPath:" + baseDirectory);
                        lstArguments.Add("/Collect:DatadogCoverage");
                    }
                    else
                    {
                        lstArguments.Insert(doubleDashIndex, "/TestAdapterPath:" + baseDirectory);
                        lstArguments.Insert(doubleDashIndex + 1, "/Collect:DatadogCoverage");
                    }

                    Log.Debug("DatadogCoverage data collector added as a command argument");
                }
                else
                {
                    Log.Warning("RunCiCommand: Code coverage is enabled but the command is not a 'dotnet test' nor 'dotnet vstest' nor 'vstest.console' command. Code coverage will not be collected.");
                }

                // Sets the code coverage path to store the json files for each module in case we are not skipping test (global coverage is reliable).
                if (!testSkippingEnabled)
                {
                    var outputFolders = new[] { Environment.CurrentDirectory, Path.GetTempPath(), };
                    foreach (var folder in outputFolders)
                    {
                        var outputPath = Path.Combine(folder, $"datadog-coverage-{DateTime.Now:yyyy-MM-dd_HH_mm_ss}");
                        if (!Directory.Exists(outputPath))
                        {
                            try
                            {
                                Directory.CreateDirectory(outputPath);
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
                Log.Warning("RunCiCommand: Code coverage is enabled but the command is not a 'dotnet' nor 'vstest.console' command. Code coverage will not be collected.");
            }
        }

        return new InitResults(true, lstArguments, profilerEnvironmentVariables, testSkippingEnabled, codeCoverageEnabled, uploadRepositoryChangesTask);
    }

    public class InitResults
    {
        private readonly Task _uploadRepositoryChangesTask;

        public InitResults(bool success, ICollection<string> arguments, Dictionary<string, string>? profilerEnvironmentVariables, bool testSkippingEnabled, bool codeCoverageEnabled, Task uploadRepositoryChangesTask)
        {
            Success = success;
            Arguments = arguments;
            ProfilerEnvironmentVariables = profilerEnvironmentVariables;
            TestSkippingEnabled = testSkippingEnabled;
            CodeCoverageEnabled = codeCoverageEnabled;
            _uploadRepositoryChangesTask = uploadRepositoryChangesTask;
        }

        public bool Success { get; }

        public ICollection<string> Arguments { get; }

        public Dictionary<string, string>? ProfilerEnvironmentVariables { get; }

        public bool TestSkippingEnabled { get; }

        public bool CodeCoverageEnabled { get; }

        public async Task UploadRepositoryChangesTask()
        {
            Log.Debug("RunCiCommand: Awaiting for the Git repository upload.");
            await _uploadRepositoryChangesTask.ConfigureAwait(false);
        }
    }
}
