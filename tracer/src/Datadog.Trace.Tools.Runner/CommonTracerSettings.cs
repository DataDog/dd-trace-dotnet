// <copyright file="CommonTracerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal class CommonTracerSettings : CommandSettings
    {
        [Description("Sets the environment name for the unified service tagging.")]
        [CommandOption("--dd-env <ENVIRONMENT>")]
        public string Environment { get; set; }

        [Description("Sets the service name for the unified service tagging.")]
        [CommandOption("--dd-service <SERVICE>")]
        public string Service { get; set; }

        [Description("Sets the version name for the unified service tagging.")]
        [CommandOption("--dd-version <VERSION>")]
        public string Version { get; set; }

        [Description("Datadog trace agent url.")]
        [CommandOption("--agent-url <URL>")]
        public string AgentUrl { get; set; }

        [Description("Sets the tracer home folder path.")]
        [CommandOption("--tracer-home <PATH>")]
        public string TracerHome { get; set; }
    }
}
