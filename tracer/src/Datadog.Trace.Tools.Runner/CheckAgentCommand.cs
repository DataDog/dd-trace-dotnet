// <copyright file="CheckAgentCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Tools.Runner.Checks;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner
{
    internal class CheckAgentCommand : Command
    {
        private readonly Argument<string?> _urlArgument = new("url") { Arity = ArgumentArity.ZeroOrOne };

        public CheckAgentCommand()
            : base("agent")
        {
            AddArgument(_urlArgument);

            AddValidator(Validate);

            this.SetHandler(ExecuteAsync);
        }

        private async Task ExecuteAsync(InvocationContext context)
        {
            ExporterSettings configuration;

            var url = _urlArgument.GetValue(context);

            if (url == null)
            {
                // Try to autodetect the agent settings
                configuration = new ExporterSettings(new EnvironmentConfigurationSourceInternal(), NullConfigurationTelemetry.Instance);

                AnsiConsole.WriteLine("No Agent URL provided, using environment variables");
            }
            else
            {
                configuration = new ExporterSettings(source: null, NullConfigurationTelemetry.Instance) { AgentUriInternal = new Uri(url) };
            }

            var result = await AgentConnectivityCheck.RunAsync(new ImmutableExporterSettings(configuration, true)).ConfigureAwait(false);

            if (!result)
            {
                context.ExitCode = 1;
                return;
            }

            Utils.WriteSuccess("Connected successfully to the Agent.");
        }

        private void Validate(CommandResult commandResult)
        {
            var url = commandResult.GetValueForArgument(_urlArgument);

            if (url != null)
            {
                try
                {
                    _ = new Uri(url);
                }
                catch (UriFormatException ex)
                {
                    commandResult.ErrorMessage = ex.Message;
                }
            }
        }
    }
}
