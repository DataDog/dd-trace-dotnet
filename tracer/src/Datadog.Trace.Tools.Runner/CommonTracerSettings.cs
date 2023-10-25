// <copyright file="CommonTracerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine;

namespace Datadog.Trace.Tools.Runner
{
    internal class CommonTracerSettings
    {
        public CommonTracerSettings(Command command)
        {
            command.AddOption(Environment);
            command.AddOption(Service);
            command.AddOption(Version);
            command.AddOption(AgentUrl);
            command.AddOption(TracerHome);
        }

        public Option<string> Environment { get; } = new("--dd-env", "Sets the environment name for the unified service tagging.");

        public Option<string> Service { get; } = new("--dd-service", "Sets the service name for the unified service tagging.");

        public Option<string> Version { get; } = new("--dd-version", "Sets the version name for the unified service tagging.");

        public Option<string> AgentUrl { get; } = new("--agent-url", "Datadog trace agent url.");

        public Option<string> TracerHome { get; } = new("--tracer-home", "Sets the tracer home folder path.");
    }
}
