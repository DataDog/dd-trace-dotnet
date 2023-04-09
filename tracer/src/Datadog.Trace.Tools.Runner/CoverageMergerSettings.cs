// <copyright file="CoverageMergerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner;

internal class CoverageMergerSettings : CommandSettings
{
    [Description("Sets the folder path where the code coverage json files are located.")]
    [CommandArgument(0, "<input-folder>")]
    public string InputFolder { get; set; }

    [Description("Sets the output json file.")]
    [CommandArgument(1, "<output-file>")]
    public string OutputFile { get; set; }
}
