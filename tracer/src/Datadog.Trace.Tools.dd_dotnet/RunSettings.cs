// <copyright file="RunSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Datadog.Trace.Tools.dd_dotnet
{
    internal class RunSettings
    {
        public RunSettings(Command command)
        {
            command.AddArgument(Command);
            command.AddOption(AdditionalEnvironmentVariables);
            command.AddOption(Environment);
            command.AddOption(Service);
            command.AddOption(Version);
            command.AddOption(AgentUrl);
            command.AddOption(TracerHome);

            command.AddValidator(Validate);
        }

        public Argument<string[]> Command { get; } = new("command", CommandLineHelpers.ParseArrayArgument, isDefault: true);

        public Option<string[]> AdditionalEnvironmentVariables { get; } = new("--set-env", CommandLineHelpers.ParseArrayArgument, isDefault: true, description: "Sets environment variables for the target command.");

        public Option<string> Environment { get; } = new("--dd-env", "Sets the environment name for the unified service tagging.");

        public Option<string> Service { get; } = new("--dd-service", "Sets the service name for the unified service tagging.");

        public Option<string> Version { get; } = new("--dd-version", "Sets the version name for the unified service tagging.");

        public Option<string> AgentUrl { get; } = new("--agent-url", "Datadog trace agent url.");

        public Option<string> TracerHome { get; } = new("--tracer-home", "Sets the tracer home folder path.");

        private void Validate(CommandResult target)
        {
            var environmentVariables = target.GetValueForOption(AdditionalEnvironmentVariables);

            foreach (var variable in environmentVariables!)
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
