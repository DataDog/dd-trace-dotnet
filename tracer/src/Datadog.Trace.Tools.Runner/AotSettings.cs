// <copyright file="AotSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal class AotSettings : CommandSettings
    {
        [CommandArgument(0, "<input-folder>")]
        public string InputFolder { get; set; }

        [CommandArgument(1, "<output-folder>")]
        public string OutputFolder { get; set; }
    }
}
