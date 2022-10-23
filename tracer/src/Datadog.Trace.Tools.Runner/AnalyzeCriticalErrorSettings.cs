// <copyright file="AnalyzeCriticalErrorSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal class AnalyzeCriticalErrorSettings : CommandSettings
    {
        [CommandArgument(0, "[pid]")]
        public int? Pid { get; set; }

        [CommandArgument(1, "[LogDirectory]")]
        public string? LogDirectory { get; set; }

        [CommandArgument(2, "[method]")]
        public string? Method { get; set; }

        [CommandArgument(3, "[module]")]
        public string? Module { get; set; }
    }
}
