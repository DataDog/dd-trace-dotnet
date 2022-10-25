// <copyright file="AnalyzeInstrumentationErrorsSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal class AnalyzeInstrumentationErrorsSettings : CommandSettings
    {
        [CommandArgument(0, "[name]")]
        public string? ProcessName { get; set; }

        [CommandArgument(1, "[pid]")]
        public int? Pid { get; set; }

        [CommandArgument(2, "[log-dir]")]
        public string? LogDirectory { get; set; }

        [CommandArgument(3, "[method]")]
        public string? Method { get; set; }

        [CommandArgument(4, "[module]")]
        public string? Module { get; set; }
    }
}
