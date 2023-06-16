// <copyright file="CheckAgentCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
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
                configuration = new ExporterSettings(new EnvironmentConfigurationSourceInternal(), NullConfigurationTelemetry.Instance);

                AnsiConsole.WriteLine("No Agent URL provided, using environment variables");
            }
            else
            {
                configuration = new ExporterSettings(source: null, NullConfigurationTelemetry.Instance) { AgentUriInternal = new Uri(settings.Url) };
            }

            var result = await AgentConnectivityCheck.RunAsync(new ImmutableExporterSettings(configuration)).ConfigureAwait(false);

            if (!result)
            {
                return 1;
            }

            Utils.WriteSuccess("Connected successfully to the Agent.");

            return 0;
        }
    }
}
