// <copyright file="RunSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Datadog.Trace.Tools.Runner
{
    internal class RunSettings : CommonTracerSettings
    {
        public RunSettings(Command command)
            : base(command)
        {
            command.AddArgument(Command);
            command.AddOption(AdditionalEnvironmentVariables);
            command.AddValidator(Validate);
        }

        public Argument<string[]> Command { get; } = new("command", CommandLineHelpers.ParseArrayArgument, isDefault: false);

        public Option<string[]> AdditionalEnvironmentVariables { get; } = new("--set-env", CommandLineHelpers.ParseArrayArgument, description: "Sets environment variables for the target command.");

        private void Validate(CommandResult target)
        {
            var environmentVariables = target.GetValueForOption(AdditionalEnvironmentVariables);

            foreach (var variable in environmentVariables)
            {
                if (!variable.Contains('='))
                {
                    target.ErrorMessage = $"Badly formatted environment variable: {variable}";
                    return;
                }
            }

            var command = target.GetValueForArgument(Command);

            if (command.Length == 0)
            {
                target.ErrorMessage = "Empty command";
            }
        }
    }
}
