// <copyright file="ConfigureCiCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner
{
    internal class ConfigureCiCommand : CommandWithExamples
    {
        private static readonly IReadOnlyDictionary<string, CIName> CiNamesMapping
            = new Dictionary<string, CIName>(StringComparer.OrdinalIgnoreCase)
            {
                ["azp"] = CIName.AzurePipelines,
                ["jenkins"] = CIName.Jenkins,
                ["github"] = CIName.GithubActions,
            };

        private readonly ApplicationContext _applicationContext;
        private readonly CommonTracerSettings _commonTracerSettings;
        private readonly Argument<string> _nameArgument = new("ci-name") { Arity = ArgumentArity.ZeroOrOne };

        public ConfigureCiCommand(ApplicationContext applicationContext)
            : base("configure", "Set the environment variables for the CI")
        {
            _applicationContext = applicationContext;
            AddArgument(_nameArgument);

            _commonTracerSettings = new(this);

            AddExample("dd-trace ci configure azp");
            AddExample("dd-trace ci configure jenkins");
            AddExample("dd-trace ci configure github");

            this.SetHandler(ExecuteAsync);
        }

        private static bool TryExtractCiName(string name, out CIName? ciName)
        {
            ciName = null;

            if (string.IsNullOrEmpty(name))
            {
                return true;
            }

            if (CiNamesMapping.TryGetValue(name, out var mappedCiName))
            {
                ciName = mappedCiName;
                return true;
            }

            Utils.WriteError($"Unsupported CI name: {name}. The supported values are: {string.Join(", ", CiNamesMapping.Keys)}.");
            return false;
        }

        private async Task ExecuteAsync(InvocationContext context)
        {
            var name = _nameArgument.GetValue(context);

            // Initialize and configure CI Visibility for this command
            var initResults = await CiUtils.InitializeCiCommandsAsync(_applicationContext, context, _commonTracerSettings, null, string.Empty, [], true).ConfigureAwait(false);
            if (!initResults.Success)
            {
                return;
            }

            if (!TryExtractCiName(name, out var ciName))
            {
                context.ExitCode = 1;
                return;
            }

            AnsiConsole.WriteLine("Setting up the environment variables.");

            if (!CIConfiguration.SetupCIEnvironmentVariables(initResults.ProfilerEnvironmentVariables, ciName))
            {
                context.ExitCode = 1;
                return;
            }

            context.ExitCode = 0;
        }
    }
}
