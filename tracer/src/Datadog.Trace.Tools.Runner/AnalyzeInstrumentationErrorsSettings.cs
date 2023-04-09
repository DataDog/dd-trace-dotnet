// <copyright file="AnalyzeInstrumentationErrorsSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal class AnalyzeInstrumentationErrorsSettings : CommandSettings
    {
        [Description("Sets the process name.")]
        [CommandOption("--process-name <NAME>")]
        public string? ProcessName { get; set; }

        [Description("Sets the process ID.")]
        [CommandOption("--pid <PID>")]
        public int? Pid { get; set; }

        [Description("Sets the instrumentation log folder path.")]
        [CommandOption("--log-path <PATH>")]
        public string? LogDirectory { get; set; }
    }
}
