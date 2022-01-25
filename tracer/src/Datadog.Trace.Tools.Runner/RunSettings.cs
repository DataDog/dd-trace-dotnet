// <copyright file="RunSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal class RunSettings : CommonTracerSettings
    {
        [CommandArgument(0, "[command]")]
        public string[] Command { get; set; }

        [CommandOption("--set-env")]
        public string[] AdditionalEnvironmentVariables { get; set; }
    }
}
