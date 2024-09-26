// <copyright file="LegacyCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner
{
    internal class LegacyCommand : Command
    {
        private readonly Argument<string[]> _commandArgument = new("command", "Command to be wrapped by the cli tool.");
        private readonly LegacySettings _legacySettings;

        public LegacyCommand(ApplicationContext applicationContext)
            : base("command")
        {
            ApplicationContext = applicationContext;

            AddArgument(_commandArgument);
            _legacySettings = new(this);

            AddValidator(Validate);
            this.SetHandler(Execute);
        }

        protected ApplicationContext ApplicationContext { get; }

        private void Execute(InvocationContext context)
        {
            var args = _commandArgument.GetValue(context);

            var profilerEnvironmentVariables = Utils.GetProfilerEnvironmentVariables(
                context,
                ApplicationContext.RunnerFolder,
                ApplicationContext.Platform,
                _legacySettings,
                ciVisibilityOptions: Utils.CIVisibilityOptions.None);

            if (profilerEnvironmentVariables is null)
            {
                context.ExitCode = 1;
                return;
            }

            var enableCIVisibilityMode = _legacySettings.EnableCIVisibilityModeOption.GetValue(context);

            // We try to autodetect the CI Visibility Mode
            if (!enableCIVisibilityMode)
            {
                // Support for VSTest.Console.exe and dotcover
                if (args.Length > 0 && (
                    string.Equals(args[0], "VSTest.Console", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args[0], "dotcover", StringComparison.OrdinalIgnoreCase)))
                {
                    enableCIVisibilityMode = true;
                }

                // Support for dotnet test and dotnet vstest command
                if (args.Length > 1 && string.Equals(args[0], "dotnet", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(args[1], "test", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(args[1], "vstest", StringComparison.OrdinalIgnoreCase))
                    {
                        enableCIVisibilityMode = true;
                    }
                }
            }

            if (enableCIVisibilityMode)
            {
                // Enable CI Visibility mode by configuration
                profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.Enabled] = "1";
            }

            var setEnvironmentVariables = _legacySettings.SetEnvironmentVariablesOption.GetValue(context);
            var crankImportFile = _legacySettings.CrankImportFileOption.GetValue(context);
            var agentUrl = _legacySettings.AgentUrlOption.GetValue(context);

            if (setEnvironmentVariables)
            {
                AnsiConsole.WriteLine("Setting up the environment variables.");
                CIConfiguration.SetupCIEnvironmentVariables(profilerEnvironmentVariables, null);
            }
            else if (!string.IsNullOrEmpty(crankImportFile))
            {
                context.ExitCode = Crank.Importer.Process(crankImportFile);
                return;
            }
            else
            {
                string cmdLine = string.Join(' ', args);
                if (!string.IsNullOrWhiteSpace(cmdLine))
                {
                    // CI Visibility mode is enabled.
                    // If the agentless feature flag is enabled, we check for ApiKey
                    // If the agentless feature flag is disabled, we check if we have connection to the agent before running the process.
                    if (enableCIVisibilityMode)
                    {
                        var ciVisibilitySettings = Ci.Configuration.CIVisibilitySettings.FromDefaultSources();
                        if (ciVisibilitySettings.Agentless)
                        {
                            if (string.IsNullOrWhiteSpace(ciVisibilitySettings.ApiKey))
                            {
                                Utils.WriteError("An API key is required in Agentless mode.");
                                context.ExitCode = 1;
                                return;
                            }
                        }
                        else if (AsyncUtil.RunSync(() => Utils.CheckAgentConnectionAsync(agentUrl)).Configuration is null)
                        {
                            context.ExitCode = 1;
                            return;
                        }
                    }

                    if (GlobalSettings.Instance.DebugEnabledInternal)
                    {
                        Console.WriteLine("Running: {0}", cmdLine);
                    }

                    var arguments = args.Length > 1 ? string.Join(' ', args.Skip(1).ToArray()) : null;

                    if (Program.CallbackForTests != null)
                    {
                        Program.CallbackForTests(args[0], arguments, profilerEnvironmentVariables);
                        return;
                    }

                    var processInfo = Utils.GetProcessStartInfo(args[0], Environment.CurrentDirectory, profilerEnvironmentVariables);

                    if (args.Length > 1)
                    {
                        processInfo.Arguments = arguments;
                    }

                    context.ExitCode = Utils.RunProcess(processInfo, ApplicationContext.TokenSource.Token);
                    return;
                }
            }
        }

        private void Validate(CommandResult commandResult)
        {
            var args = commandResult.GetValueForArgument(_commandArgument);
            var setEnvironmentVariables = commandResult.GetValueForOption(_legacySettings.SetEnvironmentVariablesOption);

            if (args.Length == 0 && !setEnvironmentVariables)
            {
                commandResult.ErrorMessage = "No command was specified";
            }
        }
    }
}
