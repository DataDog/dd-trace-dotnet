// <copyright file="RunCiSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal class RunCiSettings : RunSettings
    {
        [Description("Enables agentless with the Api Key")]
        [CommandOption("--api-key <APIKEY>")]
        public string ApiKey { get; set; }
    }
}
