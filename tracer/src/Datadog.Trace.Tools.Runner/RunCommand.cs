// <copyright file="RunCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal class RunCommand : Command<RunSettings>
    {
        public RunCommand(ApplicationContext applicationContext)
        {
            ApplicationContext = applicationContext;
        }

        protected ApplicationContext ApplicationContext { get; }

        public override int Execute(CommandContext context, RunSettings settings)
        {
            return Execute(context, settings, enableCiMode: false);
        }

        public override ValidationResult Validate(CommandContext context, RunSettings settings)
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

            return base.Validate(context, settings);
        }

        protected int Execute(CommandContext context, RunSettings settings, bool enableCiMode)
        {
            var args = settings.Command ?? context.Remaining.Raw;

            var profilerEnvironmentVariables = Utils.GetProfilerEnvironmentVariables(
                ApplicationContext.RunnerFolder,
                ApplicationContext.Platform,
                settings);

            if (profilerEnvironmentVariables is null)
            {
                return 1;
            }

            if (enableCiMode)
            {
                profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.Enabled] = "1";
            }

            if (settings.AdditionalEnvironmentVariables != null)
            {
                foreach (var env in settings.AdditionalEnvironmentVariables)
                {
                    var (key, value) = ParseEnvironmentVariable(env);

                    profilerEnvironmentVariables[key] = value;
                }
            }

            // CI Visibility mode is enabled.
            // If the agentless feature flag is enabled, we check for ApiKey
            // If the agentless feature flag is disabled, we check if we have connection to the agent before running the process.
            if (enableCiMode)
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
                else if (!Utils.CheckAgentConnectionAsync(settings.AgentUrl).GetAwaiter().GetResult())
                {
                    return 1;
                }
            }

            AnsiConsole.WriteLine("Running: " + string.Join(' ', args));

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

        private static (string Key, string Value) ParseEnvironmentVariable(string env)
        {
            var values = env.Split('=', 2);

            return (values[0], values[1]);
        }
    }
}
