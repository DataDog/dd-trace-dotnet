// <copyright file="LegacyCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal class LegacyCommand : Command<LegacySettings>
    {
        public LegacyCommand(ApplicationContext applicationContext)
        {
            ApplicationContext = applicationContext;
        }

        protected ApplicationContext ApplicationContext { get; }

        public override int Execute(CommandContext context, LegacySettings options)
        {
            var args = options.Command ?? context.Remaining.Raw;

            // Start logic

            var profilerEnvironmentVariables = Utils.GetProfilerEnvironmentVariables(
                ApplicationContext.RunnerFolder,
                ApplicationContext.Platform,
                options);

            if (profilerEnvironmentVariables is null)
            {
                return 1;
            }

            // We try to autodetect the CI Visibility Mode
            if (!options.EnableCIVisibilityMode)
            {
                // Support for VSTest.Console.exe and dotcover
                if (args.Count > 0 && (
                    string.Equals(args[0], "VSTest.Console", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args[0], "dotcover", StringComparison.OrdinalIgnoreCase)))
                {
                    options.EnableCIVisibilityMode = true;
                }

                // Support for dotnet test and dotnet vstest command
                if (args.Count > 1 && string.Equals(args[0], "dotnet", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(args[1], "test", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(args[1], "vstest", StringComparison.OrdinalIgnoreCase))
                    {
                        options.EnableCIVisibilityMode = true;
                    }
                }
            }

            if (options.EnableCIVisibilityMode)
            {
                // Enable CI Visibility mode by configuration
                profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.Enabled] = "1";
            }

            if (options.SetEnvironmentVariables)
            {
                AnsiConsole.WriteLine("Setting up the environment variables.");
                CIConfiguration.SetupCIEnvironmentVariables(profilerEnvironmentVariables, null);
            }
            else if (!string.IsNullOrEmpty(options.CrankImportFile))
            {
                return Crank.Importer.Process(options.CrankImportFile);
            }
            else
            {
                string cmdLine = string.Join(' ', args);
                if (!string.IsNullOrWhiteSpace(cmdLine))
                {
                    // CI Visibility mode is enabled.
                    // If the agentless feature flag is enabled, we check for ApiKey
                    // If the agentless feature flag is disabled, we check if we have connection to the agent before running the process.
                    if (options.EnableCIVisibilityMode)
                    {
                        var ciVisibilitySettings = Ci.Configuration.CIVisibilitySettings.FromDefaultSources();
                        if (ciVisibilitySettings.Agentless)
                        {
                            if (string.IsNullOrWhiteSpace(ciVisibilitySettings.ApiKey))
                            {
                                Utils.WriteError("An API key is required in Agentless mode.");
                                return 1;
                            }
                        }
                        else if (!Utils.CheckAgentConnectionAsync(options.AgentUrl).GetAwaiter().GetResult())
                        {
                            return 1;
                        }
                    }

                    AnsiConsole.WriteLine("Running: " + cmdLine);

                    var arguments = args.Count > 1 ? string.Join(' ', args.Skip(1).ToArray()) : null;

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

                    return Utils.RunProcess(processInfo, ApplicationContext.TokenSource.Token);
                }
            }

            return 0;
        }

        public override ValidationResult Validate(CommandContext context, LegacySettings settings)
        {
            var args = settings.Command ?? context.Remaining.Raw;

            if (args.Count == 0 && !settings.SetEnvironmentVariables)
            {
                return ValidationResult.Error("No command was specified");
            }

            return base.Validate(context, settings);
        }
    }
}
