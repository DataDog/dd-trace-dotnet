// <copyright file="RunHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Util;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal static class RunHelper
    {
        public static int Execute(ApplicationContext applicationContext, CommandContext context, RunSettings settings)
        {
            var args = settings.Command ?? context.Remaining.Raw;

            var profilerEnvironmentVariables = Utils.GetProfilerEnvironmentVariables(
                applicationContext.RunnerFolder,
                applicationContext.Platform,
                settings);

            if (profilerEnvironmentVariables is null)
            {
                return 1;
            }

            if (settings.AdditionalEnvironmentVariables != null)
            {
                foreach (var env in settings.AdditionalEnvironmentVariables)
                {
                    var (key, value) = ParseEnvironmentVariable(env);

                    profilerEnvironmentVariables[key] = value;
                }
            }

            var arguments = args.Count > 1 ? Utils.GetArgumentsAsString(args.Skip(1).ToArray()) : null;

            // CI Visibility mode is enabled.
            // If the agentless feature flag is enabled, we check for ApiKey
            // If the agentless feature flag is disabled, we check if we have connection to the agent before running the process.
            var createTestSession = false;
            var testSkippingEnabled = false;
            var codeCoverageEnabled = false;
            string codeCoveragePath = null;
            if (settings is RunCiSettings ciSettings)
            {
                var ciVisibilitySettings = Ci.Configuration.CIVisibilitySettings.FromDefaultSources();
                var agentless = ciVisibilitySettings.Agentless;
                var apiKey = ciVisibilitySettings.ApiKey;
                var applicationKey = ciVisibilitySettings.ApplicationKey;

                profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.Enabled] = "1";
                if (!string.IsNullOrEmpty(ciSettings?.ApiKey))
                {
                    agentless = true;
                    apiKey = ciSettings.ApiKey;
                    profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.AgentlessEnabled] = "1";
                    profilerEnvironmentVariables[Configuration.ConfigurationKeys.ApiKey] = ciSettings.ApiKey;
                }

                AgentConfiguration agentConfiguration = null;
                if (agentless)
                {
                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        Utils.WriteError("An API key is required in Agentless mode.");
                        return 1;
                    }
                }
                else
                {
                    agentConfiguration = AsyncUtil.RunSync(() => Utils.CheckAgentConnectionAsync(settings.AgentUrl));
                    if (agentConfiguration is null)
                    {
                        return 1;
                    }
                }

                codeCoverageEnabled = ciVisibilitySettings.CodeCoverageEnabled == true || ciVisibilitySettings.TestsSkippingEnabled == true;
                testSkippingEnabled = ciVisibilitySettings.TestsSkippingEnabled == true;

                if (agentless || !string.IsNullOrEmpty(agentConfiguration.EventPlatformProxyEndpoint))
                {
                    // We can activate all features here (Agentless or EVP proxy mode)
                    createTestSession = true;
                    if (!agentless)
                    {
                        // EVP proxy is enabled.
                        // By setting the environment variables we avoid the usage of the DiscoveryService in each child process
                        // to ask for EVP proxy support.
                        profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.ForceAgentsEvpProxy] = "1";
                        EnvironmentHelpers.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ForceAgentsEvpProxy, "1");
                    }

                    // If we have api and application key, and the code coverage or the tests skippeable environment variables
                    // are not set when the intelligent test runner is enabled, we query the settings api to check if it should enable coverage or not.
                    var useConfigurationApi = !agentless || !string.IsNullOrEmpty(applicationKey);
                    if (useConfigurationApi
                        && ciVisibilitySettings.IntelligentTestRunnerEnabled
                        && (ciVisibilitySettings.CodeCoverageEnabled == null || ciVisibilitySettings.TestsSkippingEnabled == null))
                    {
                        try
                        {
                            var itrClient = new Ci.IntelligentTestRunnerClient(Ci.CIEnvironmentValues.Instance.WorkspacePath, ciVisibilitySettings);
                            // we skip the framework info because we are interested in the target projects info not the runner one.
                            var itrSettings = AsyncUtil.RunSync(() => itrClient.GetSettingsAsync(skipFrameworkInfo: true));
                            codeCoverageEnabled = itrSettings.CodeCoverage == true || itrSettings.TestsSkipping == true;
                            testSkippingEnabled = itrSettings.TestsSkipping == true;
                        }
                        catch (Exception ex)
                        {
                            CIVisibility.Log.Warning(ex, ex.Message);
                        }
                    }
                }

                if (codeCoverageEnabled)
                {
                    // Check if we are running dotnet process
                    if (string.Equals(args[0], "dotnet", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[0], "VSTest.Console", StringComparison.OrdinalIgnoreCase))
                    {
                        var isTestCommand = false;
                        var isVsTestCommand = string.Equals(args[0], "VSTest.Console", StringComparison.OrdinalIgnoreCase);
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
                        var baseDirectory = Path.GetDirectoryName(typeof(Coverage.Collector.CoverageCollector).Assembly.Location);
                        if (isTestCommand)
                        {
                            arguments += " --collect DatadogCoverage --test-adapter-path \"" + baseDirectory + "\"";
                        }
                        else if (isVsTestCommand)
                        {
                            arguments += " /Collect:DatadogCoverage /TestAdapterPath:\"" + baseDirectory + "\"";
                        }

                        // Sets the code coverage path to store the json files for each module.
                        var lstOutputFolders = new List<string>
                        {
                            Environment.CurrentDirectory,
                            Path.GetTempPath(),
                        };

                        foreach (var folder in lstOutputFolders)
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
            }

            var command = $"{args[0]} {arguments}".Trim();
            TestSession session = null;
            if (createTestSession && Program.CallbackForTests is null)
            {
                session = TestSession.GetOrCreate(command, null, null, null, true);
                session.SetTag(CommonTags.TestsSkippingEnabled, testSkippingEnabled ? "true" : "false");
                session.SetTag(CommonTags.CodeCoverageEnabled, codeCoverageEnabled ? "true" : "false");

                // At session level we know if the ITR is disabled (meaning that no tests will be skipped)
                // In that case we tell the backend no tests are going to be skipped.
                if (!testSkippingEnabled)
                {
                    session.SetTag(CommonTags.TestsSkipped, "false");
                }
            }

            var exitCode = 0;
            try
            {
                AnsiConsole.WriteLine("Running: " + command);

                if (Program.CallbackForTests != null)
                {
                    Program.CallbackForTests(args[0], arguments, profilerEnvironmentVariables);
                    return 0;
                }

                var processInfo = Utils.GetProcessStartInfo(args[0], Environment.CurrentDirectory, profilerEnvironmentVariables);

                if (args.Count > 1)
                {
                    processInfo.Arguments = arguments;
                }

                exitCode = Utils.RunProcess(processInfo, applicationContext.TokenSource.Token);
                session?.SetTag(TestTags.CommandExitCode, exitCode);
                return exitCode;
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
                        var outputPath = Path.Combine(codeCoveragePath, $"session-coverage-{DateTime.Now:yyyy-MM-dd_HH_mm_ss}.json");
                        if (CoverageUtils.TryCombineAndGetTotalCoverage(codeCoveragePath, outputPath, out var globalCoverage, useStdOut: false) &&
                            globalCoverage is not null)
                        {
                            // Adds the global code coverage percentage to the session
                            session.SetTag(CommonTags.CodeCoverageTotalLines, globalCoverage.Data[0].ToString(CultureInfo.InvariantCulture));
                        }
                    }

                    session.Close(exitCode == 0 ? TestStatus.Pass : TestStatus.Fail);
                }
            }
        }

        public static ValidationResult Validate(CommandContext context, RunSettings settings)
        {
            var args = settings.Command ?? context.Remaining.Raw;

            if (args.Count == 0)
            {
                return ValidationResult.Error("Missing command");
            }

            if (settings.AdditionalEnvironmentVariables != null)
            {
                foreach (var env in settings.AdditionalEnvironmentVariables)
                {
                    if (!env.Contains('='))
                    {
                        return ValidationResult.Error($"Badly formatted environment variable: {env}");
                    }
                }
            }

            return ValidationResult.Success();
        }

        private static (string Key, string Value) ParseEnvironmentVariable(string env)
        {
            var values = env.Split('=', 2);

            return (values[0], values[1]);
        }
    }
}
