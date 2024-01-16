// <copyright file="RunCiCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner
{
    internal class RunCiCommand : CommandWithExamples
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RunCiCommand));

        private readonly ApplicationContext _applicationContext;
        private readonly RunSettings _runSettings;
        private readonly Option<string> _apiKeyOption = new("--api-key", "Enables agentless with the Api Key");

        public RunCiCommand(ApplicationContext applicationContext)
            : base("run", "Run a command in the CI and instrument the tests")
        {
            _applicationContext = applicationContext;

            _runSettings = new(this);
            AddOption(_apiKeyOption);

            AddExample("dd-trace ci run -- dotnet test");

            this.SetHandler(ExecuteAsync);
        }

        private async Task ExecuteAsync(InvocationContext context)
        {
            // CI Visibility mode is enabled.
            var args = _runSettings.Command.GetValue(context);
            var program = args[0];
            var arguments = args.Length > 1 ? Utils.GetArgumentsAsString(args.Skip(1)) : string.Empty;

            // Get profiler environment variables
            if (!RunHelper.TryGetEnvironmentVariables(_applicationContext, context, _runSettings, out var profilerEnvironmentVariables))
            {
                context.ExitCode = 1;
                return;
            }

            // We force CIVisibility mode on child process
            profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.Enabled] = "1";

            // We check the settings and merge with the command settings options
            var ciVisibilitySettings = CIVisibility.Settings;
            var agentless = ciVisibilitySettings.Agentless;
            var apiKey = ciVisibilitySettings.ApiKey;

            var customApiKey = _apiKeyOption.GetValue(context);

            if (!string.IsNullOrEmpty(customApiKey))
            {
                agentless = true;
                apiKey = customApiKey;
                profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.AgentlessEnabled] = "1";
                profilerEnvironmentVariables[Configuration.ConfigurationKeys.ApiKey] = customApiKey;
            }

            var agentUrl = _runSettings.AgentUrl.GetValue(context);

            // If the agentless feature flag is enabled, we check for ApiKey
            // If the agentless feature flag is disabled, we check if we have connection to the agent before running the process.
            AgentConfiguration agentConfiguration = null;
            IDiscoveryService discoveryService = NullDiscoveryService.Instance;
            if (agentless)
            {
                Log.Debug("RunCiCommand: Agentless has been enabled. Checking API key");
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    Utils.WriteError("An API key is required in Agentless mode.");
                    Log.Error("RunHelper: An API key is required in Agentless mode.");
                    context.ExitCode = 1;
                    return;
                }
            }
            else
            {
                Log.Debug("RunCiCommand: Agent-based mode has been enabled. Checking agent connection.");
                (agentConfiguration, discoveryService) = await Utils.CheckAgentConnectionAsync(agentUrl).ConfigureAwait(false);
                if (agentConfiguration is null)
                {
                    Log.Error("RunCiCommand: Agent configuration cannot be retrieved.");
                    context.ExitCode = 1;
                    return;
                }
            }

            var uploadRepositoryChangesTask = Task.CompletedTask;

            // Set Agentless configuration from the command line options
            ciVisibilitySettings.SetAgentlessConfiguration(agentless, apiKey, ciVisibilitySettings.AgentlessUrl);

            if (!string.IsNullOrEmpty(agentUrl))
            {
                EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.AgentUri, agentUrl);
            }

            // Initialize flags to enable code coverage and test skipping
            var codeCoverageEnabled = ciVisibilitySettings.CodeCoverageEnabled == true || ciVisibilitySettings.TestsSkippingEnabled == true;
            var testSkippingEnabled = ciVisibilitySettings.TestsSkippingEnabled == true;

            var createTestSession = false;
            string codeCoveragePath = null;
            var hasEvpProxy = !string.IsNullOrEmpty(agentConfiguration?.EventPlatformProxyEndpoint);
            if (agentless || hasEvpProxy)
            {
                // Initialize CI Visibility with the current settings
                Log.Debug("RunCiCommand: Initialize CI Visibility for the runner.");
                CIVisibility.InitializeFromRunner(ciVisibilitySettings, discoveryService, hasEvpProxy);

                // Upload git metadata by default (unless is disabled explicitly) or if ITR is enabled (required).
                Log.Debug("RunCiCommand: Uploading repository changes.");

                // Change the .git search folder to the CurrentDirectory or WorkingFolder
                CIEnvironmentValues.Instance.GitSearchFolder = Environment.CurrentDirectory;
                if (string.IsNullOrEmpty(CIEnvironmentValues.Instance.WorkspacePath))
                {
                    // In case we cannot get the WorkspacePath we fallback to the default configuration.
                    CIEnvironmentValues.Instance.GitSearchFolder = null;
                }

                var lazyItrClient = new Lazy<IntelligentTestRunnerClient>(() => new(CIEnvironmentValues.Instance.WorkspacePath, ciVisibilitySettings));
                if (ciVisibilitySettings.GitUploadEnabled != false || ciVisibilitySettings.IntelligentTestRunnerEnabled)
                {
                    // If we are in git upload only then we can defer the await until the child command exits.
                    uploadRepositoryChangesTask = Task.Run(() => lazyItrClient.Value.UploadRepositoryChangesAsync());

                    // Once the repository has been uploaded we switch off the git upload in children processes
                    profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.GitUploadEnabled] = "0";
                    EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.GitUploadEnabled, "0");
                }

                // We can activate all features here (Agentless or EVP proxy mode)
                createTestSession = true;
                if (!agentless)
                {
                    // EVP proxy is enabled.
                    // By setting the environment variables we avoid the usage of the DiscoveryService in each child process
                    // to ask for EVP proxy support.
                    profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.ForceAgentsEvpProxy] = "1";
                    EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ForceAgentsEvpProxy, "1");
                    Log.Debug("RunCiCommand: EVP proxy was detected.");
                }

                // If we have an api key, and the code coverage or the tests skippable environment variables
                // are not set when the intelligent test runner is enabled, we query the settings api to check if it should enable coverage or not.
                if (!ciVisibilitySettings.IntelligentTestRunnerEnabled)
                {
                    Log.Debug("RunCiCommand: Intelligent test runner is disabled, call to configuration api skipped.");
                }

                // If we still don't know if we have to enable code coverage or test skipping, then let's request the configuration API
                if (ciVisibilitySettings.IntelligentTestRunnerEnabled
                 && (ciVisibilitySettings.CodeCoverageEnabled == null || ciVisibilitySettings.TestsSkippingEnabled == null))
                {
                    try
                    {
                        CIVisibility.Log.Debug("RunCiCommand: Calling configuration api...");

                        // we skip the framework info because we are interested in the target projects info not the runner one.
                        var itrSettings = await lazyItrClient.Value.GetSettingsAsync(skipFrameworkInfo: true).ConfigureAwait(false);

                        // we check if the backend require the git metadata first
                        if (itrSettings.RequireGit == true)
                        {
                            Log.Debug("RunCiCommand: require git received, awaiting for the git repository upload.");
                            await uploadRepositoryChangesTask.ConfigureAwait(false);

                            Log.Debug("RunCiCommand: calling the configuration api again.");
                            itrSettings = await lazyItrClient.Value.GetSettingsAsync(skipFrameworkInfo: true).ConfigureAwait(false);
                        }

                        codeCoverageEnabled = itrSettings.CodeCoverage == true || itrSettings.TestsSkipping == true;
                        testSkippingEnabled = itrSettings.TestsSkipping == true;
                    }
                    catch (Exception ex)
                    {
                        CIVisibility.Log.Warning(ex, "Error getting ITR settings from configuration api");
                    }
                }
            }

            Log.Debug("RunCiCommand: CodeCoverageEnabled = {Value}", codeCoverageEnabled);
            Log.Debug("RunCiCommand: TestSkippingEnabled = {Value}", testSkippingEnabled);
            ciVisibilitySettings.SetCodeCoverageEnabled(codeCoverageEnabled);
            profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.CodeCoverage] = codeCoverageEnabled ? "1" : "0";

            if (!testSkippingEnabled)
            {
                // If test skipping is disabled we set this to the child process so we avoid to query the settings api again.
                // If is not disabled we need to query the backend again in the child process with more runtime info.
                ciVisibilitySettings.SetTestsSkippingEnabled(testSkippingEnabled);
                profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.TestsSkippingEnabled] = "0";
            }

            // Let's set the code coverage datacollector if the code coverage is enabled
            if (codeCoverageEnabled)
            {
                // Check if we are running dotnet process
                if (string.Equals(program, "dotnet", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(program, "VSTest.Console", StringComparison.OrdinalIgnoreCase))
                {
                    var isTestCommand = false;
                    var isVsTestCommand = string.Equals(program, "VSTest.Console", StringComparison.OrdinalIgnoreCase);
                    foreach (var arg in args.Skip(1))
                    {
                        isTestCommand |= string.Equals(arg, "test", StringComparison.OrdinalIgnoreCase);
                        isVsTestCommand |= string.Equals(arg, "vstest", StringComparison.OrdinalIgnoreCase);

                        if (isTestCommand || isVsTestCommand)
                        {
                            break;
                        }
                    }

                    // Add the Datadog coverage collector
                    var baseDirectory = AppContext.BaseDirectory;
                    if (isTestCommand)
                    {
                        arguments += " --collect DatadogCoverage --test-adapter-path \"" + baseDirectory + "\"";
                    }
                    else if (isVsTestCommand)
                    {
                        arguments += " /Collect:DatadogCoverage /TestAdapterPath:\"" + baseDirectory + "\"";
                    }

                    // Sets the code coverage path to store the json files for each module.
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
                                codeCoveragePath = outputPath;
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

            // Final command to execute
            var command = $"{program} {arguments}".Trim();

            // We create a test session if the flag is turned on (agentless or evp proxy)
            TestSession session = null;
            if (createTestSession)
            {
                session = TestSession.InternalGetOrCreate(command, null, null, null, true);
                session.SetTag(IntelligentTestRunnerTags.TestTestsSkippingEnabled, testSkippingEnabled ? "true" : "false");
                session.SetTag(CodeCoverageTags.Enabled, codeCoverageEnabled ? "true" : "false");

                // At session level we know if the ITR is disabled (meaning that no tests will be skipped)
                // In that case we tell the backend no tests are going to be skipped.
                if (!testSkippingEnabled)
                {
                    session.SetTag(IntelligentTestRunnerTags.TestsSkipped, "false");
                }
            }

            // Run child process
            var exitCode = 0;
            try
            {
                AnsiConsole.WriteLine("Running: " + command);

                if (testSkippingEnabled || Program.CallbackForTests is not null)
                {
                    // Awaiting git repository task before running the command if ITR test skipping is enabled.
                    // Test skipping requires the git upload metadata information before hand
                    Log.Debug("RunCiCommand: Awaiting for the Git repository upload.");
                    await uploadRepositoryChangesTask.ConfigureAwait(false);
                }

                if (Program.CallbackForTests is { } callbackForTests)
                {
                    callbackForTests(program, arguments, profilerEnvironmentVariables);
                    return;
                }

                Log.Debug("RunCiCommand: Launching: {Value}", command);
                var processInfo = Utils.GetProcessStartInfo(program, Environment.CurrentDirectory, profilerEnvironmentVariables);
                if (!string.IsNullOrEmpty(arguments))
                {
                    processInfo.Arguments = arguments;
                }

                exitCode = Utils.RunProcess(processInfo, _applicationContext.TokenSource.Token);
                session?.SetTag(TestTags.CommandExitCode, exitCode);
                Log.Debug<int>("RunCiCommand: Finished with exit code: {Value}", exitCode);

                if (!testSkippingEnabled)
                {
                    // Awaiting git repository task after running the command if ITR test skipping is disabled.
                    Log.Debug("RunCiCommand: Awaiting for the Git repository upload.");
                    await uploadRepositoryChangesTask.ConfigureAwait(false);
                }

                context.ExitCode = exitCode;
            }
            catch (Exception ex)
            {
                session?.SetErrorInfo(ex);
                throw;
            }
            finally
            {
                if (session is not null)
                {
                    // If the code coverage path is set we try to read all json files created, merge them into a single one and extract the
                    // global code coverage percentage.
                    // Note: we also write the total global code coverage to the `session-coverage-{date}.json` file
                    if (!string.IsNullOrEmpty(codeCoveragePath))
                    {
                        if (!string.IsNullOrEmpty(EnvironmentHelpers.GetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath)))
                        {
                            Log.Warning("DD_CIVISIBILITY_EXTERNAL_CODE_COVERAGE_PATH was ignored because DD_CIVISIBILITY_CODE_COVERAGE_ENABLED is set.");
                        }

                        var outputPath = Path.Combine(codeCoveragePath, $"session-coverage-{DateTime.Now:yyyy-MM-dd_HH_mm_ss}.json");
                        if (CoverageUtils.TryCombineAndGetTotalCoverage(codeCoveragePath, outputPath, out var globalCoverage, useStdOut: false) &&
                            globalCoverage is not null)
                        {
                            // We only report the code coverage percentage if the customer manually sets the 'DD_CIVISIBILITY_CODE_COVERAGE_ENABLED' environment variable according to the new spec.
                            if (EnvironmentHelpers.GetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage)?.ToBoolean() == true)
                            {
                                // Adds the global code coverage percentage to the session
                                session.SetTag(CodeCoverageTags.PercentageOfTotalLines, globalCoverage.GetTotalPercentage());
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            if (EnvironmentHelpers.GetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath) is { Length: > 0 } extCodeCoverageFilePath &&
                                File.Exists(extCodeCoverageFilePath))
                            {
                                // Check Code Coverage from other files.
                                var xmlDoc = new XmlDocument();
                                xmlDoc.Load(extCodeCoverageFilePath);

                                if (xmlDoc.SelectSingleNode("/CoverageSession/Summary/@sequenceCoverage") is { } seqCovAttribute &&
                                    double.TryParse(seqCovAttribute.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seqCovValue))
                                {
                                    // Found using the OpenCover format.

                                    // Adds the global code coverage percentage to the session
                                    var coveragePercentage = Math.Round(seqCovValue, 2).ToValidPercentage();
                                    session.SetTag(CodeCoverageTags.Enabled, "true");
                                    session.SetTag(CodeCoverageTags.PercentageOfTotalLines, coveragePercentage);
                                    Log.Debug("RunCiCommand: OpenCover code coverage was reported: {Value}", seqCovValue);
                                }
                                else if (xmlDoc.SelectSingleNode("/coverage/@line-rate") is { } lineRateAttribute &&
                                    double.TryParse(lineRateAttribute.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var lineRateValue))
                                {
                                    // Found using the Cobertura format.

                                    // Adds the global code coverage percentage to the session
                                    var coveragePercentage = Math.Round(lineRateValue * 100, 2).ToValidPercentage();
                                    session.SetTag(CodeCoverageTags.Enabled, "true");
                                    session.SetTag(CodeCoverageTags.PercentageOfTotalLines, coveragePercentage);
                                    Log.Debug("RunCiCommand: Cobertura code coverage was reported: {Value}", lineRateAttribute.Value);
                                }
                                else
                                {
                                    Log.Warning("RunCiCommand: Error while reading the external file code coverage. Format is not supported.");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "RunCiCommand: Error while reading the external file code coverage.");
                        }
                    }

                    await session.CloseAsync(exitCode == 0 ? TestStatus.Pass : TestStatus.Fail).ConfigureAwait(false);
                }
            }
        }
    }
}
