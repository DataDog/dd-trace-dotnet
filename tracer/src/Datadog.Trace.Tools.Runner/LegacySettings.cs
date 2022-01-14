// <copyright file="LegacySettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal class LegacySettings : CommandSettings
    {
        [CommandArgument(0, "[command]")]
        [Description("Command to be wrapped by the cli tool.")]
        public string[] Command { get; set; }

        [CommandOption("--dd-env")]
        [Description("Sets the environment name for the unified service tagging.")]
        public string Environment { get; set; }

        [CommandOption("--dd-service")]
        [Description("Sets the service name for the unified service tagging.")]
        public string Service { get; set; }

        [CommandOption("--dd-version")]
        [Description("Sets the version name for the unified service tagging.")]
        public string Version { get; set; }

        [CommandOption("--agent-url")]
        [Description("Datadog trace agent url.")]
        public string AgentUrl { get; set; }

        [CommandOption("--tracer-home")]
        [Description("Sets the tracer home folder path.")]
        public string TracerHomeFolder { get; set; }

        [CommandOption("--set-ci")]
        [Description("Setup the clr profiler environment variables for the CI job and exit. (only supported in Azure Pipelines)")]
        public bool SetEnvironmentVariables { get; set; }

        [CommandOption("--ci-visibility")]
        [Description("Run the command in CI Visibility Mode")]
        public bool EnableCIVisibilityMode { get; set; }

        [CommandOption("--env-vars")]
        [Description("Sets environment variables to the target command.")]
        public string EnvironmentValues { get; set; }

        [CommandOption("--crank-import")]
        [Description("Import crank Json results file.")]
        public string CrankImportFile { get; set; }
    }
}
