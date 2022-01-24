// <copyright file="CheckAgentCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Tools.Runner.Checks;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal class CheckAgentCommand : AsyncCommand<CheckAgentSettings>
    {
        public override async Task<int> ExecuteAsync(CommandContext context, CheckAgentSettings settings)
        {
            ExporterSettings configuration;

            if (settings.Url == null)
            {
                // Try to autodetect the agent settings
                configuration = new ExporterSettings(new EnvironmentConfigurationSource());

                AnsiConsole.WriteLine($"No agent url provided, using environment variables");
            }
            else
            {
                configuration = new ExporterSettings { AgentUri = new Uri(settings.Url) };

                // TODO: Remove when the logic has been moved to the ImmutableExporterSettings constructor
                if (settings.Url.StartsWith("unix://"))
                {
                    configuration.TracesTransport = TracesTransportType.UnixDomainSocket;
                    configuration.TracesUnixDomainSocketPath = settings.Url.Substring("unix://".Length);
                }
            }

            var result = await AgentConnectivityCheck.Run(new ImmutableExporterSettings(configuration)).ConfigureAwait(false);

            if (!result)
            {
                return 1;
            }

            Utils.WriteSuccess("No issue found with the agent.");

            return 0;
        }
    }
}
