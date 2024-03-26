// <copyright file="LegacySettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine;

namespace Datadog.Trace.Tools.Runner
{
    internal class LegacySettings
    {
        public LegacySettings(Command command)
        {
            command.AddOption(EnvironmentOption);
            command.AddOption(ServiceOption);
            command.AddOption(VersionOption);
            command.AddOption(AgentUrlOption);
            command.AddOption(TracerHomeFolderOption);
            command.AddOption(SetEnvironmentVariablesOption);
            command.AddOption(EnableCIVisibilityModeOption);
            command.AddOption(EnvironmentValuesOption);
            command.AddOption(CrankImportFileOption);
        }

        public Option<string> EnvironmentOption { get; } = new("--dd-env", "Sets the environment name for the unified service tagging.");

        public Option<string> ServiceOption { get; } = new("--dd-service", "Sets the service name for the unified service tagging.");

        public Option<string> VersionOption { get; } = new("--dd-version", "Sets the version name for the unified service tagging.");

        public Option<string> AgentUrlOption { get; } = new("--agent-url", "Datadog trace agent url.");

        public Option<string> TracerHomeFolderOption { get; } = new("--tracer-home", "Sets the tracer home folder path.");

        public Option<bool> SetEnvironmentVariablesOption { get; } = new("--set-ci", "Setup the clr profiler environment variables for the CI job and exit. (only supported in Azure Pipelines)");

        public Option<bool> EnableCIVisibilityModeOption { get; } = new("--ci-visibility", "Run the command in CI Visibility Mode.");

        public Option<string> EnvironmentValuesOption { get; } = new("--env-vars", "Sets environment variables to the target command.");

        public Option<string> CrankImportFileOption { get; } = new("--crank-import", "Import crank Json results file.");
    }
}
