// <copyright file="CommonTracerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal class CommonTracerSettings : CommandSettings
    {
        [CommandOption("--dd-env")]
        public string Environment { get; set; }

        [CommandOption("--dd-service")]
        public string Service { get; set; }

        [CommandOption("--dd-version")]
        public string Version { get; set; }

        [CommandOption("--agent-url")]
        public string AgentUrl { get; set; }

        [CommandOption("--tracer-home")]
        public string TracerHome { get; set; }
    }
}
