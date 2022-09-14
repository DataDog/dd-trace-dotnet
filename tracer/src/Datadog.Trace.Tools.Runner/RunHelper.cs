// <copyright file="RunHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

            var arguments = args.Count > 1 ? string.Join(' ', args.Skip(1).ToArray()) : null;

            // Fix: wrap arguments containing spaces with double quotes ( "[arg with spaces]" )
            FixDoubleQuotes(ref arguments);

            // CI Visibility mode is enabled.
            // If the agentless feature flag is enabled, we check for ApiKey
            // If the agentless feature flag is disabled, we check if we have connection to the agent before running the process.
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

                if (agentless)
                {
                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        Utils.WriteError("An API key is required in Agentless mode.");
                        return 1;
                    }
                }
                else if (!Utils.CheckAgentConnectionAsync(settings.AgentUrl).GetAwaiter().GetResult())
                {
                    return 1;
                }

                var enableCodeCoverage = ciVisibilitySettings.CodeCoverageEnabled == true || ciVisibilitySettings.TestsSkippingEnabled == true;

                // If we have api and application key, and the code coverage or the tests skippeable environment variables
                // are not set when the intelligent test runner is enabled, we query the settings api to check if it should enable coverage or not.
                if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(applicationKey) && ciVisibilitySettings.IntelligentTestRunnerEnabled &&
                    (ciVisibilitySettings.CodeCoverageEnabled == null || ciVisibilitySettings.TestsSkippingEnabled == null))
                {
                    var itrClient = new Ci.IntelligentTestRunnerClient(Ci.CIEnvironmentValues.Instance.WorkspacePath, ciVisibilitySettings);
                    // we should skip the framework info because we are interested in the target projects info not the runner one.
                    var itrSettings = itrClient.GetSettingsAsync(skipFrameworkInfo: true).GetAwaiter().GetResult();
                    enableCodeCoverage = itrSettings.CodeCoverage == true || itrSettings.TestsSkipping == true;
                }

                if (enableCodeCoverage)
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
                    }
                }
            }

            AnsiConsole.WriteLine("Running: " + string.Join(' ', args));

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

            return Utils.RunProcess(processInfo, applicationContext.TokenSource.Token);
        }

        /// <summary>
        /// Wrap argument values with spaces with double quotes
        /// </summary>
        /// <param name="arguments">arguments string instance</param>
        internal static void FixDoubleQuotes(ref string arguments)
        {
            if (arguments is not null)
            {
                var argumentsRegex = Regex.Matches(arguments, @"[--/][a-zA-Z-]*:?([0-9a-zA-Z :\\./_]*)");
                foreach (Match arg in argumentsRegex)
                {
                    var value = arg.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(value) && value.IndexOf(' ') > 0)
                    {
                        var replace = $"\"{value}\"";
                        arguments = arguments.Replace(value, replace);
                    }
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
