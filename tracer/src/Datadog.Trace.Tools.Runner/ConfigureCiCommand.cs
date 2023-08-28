// <copyright file="ConfigureCiCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner
{
    internal class ConfigureCiCommand : CommandWithExamples
    {
        private static readonly IReadOnlyDictionary<string, CIName> CiNamesMapping
            = new Dictionary<string, CIName>(StringComparer.OrdinalIgnoreCase)
            {
                ["azp"] = CIName.AzurePipelines
            };

        private readonly ApplicationContext _applicationContext;
        private readonly Argument<string> _nameArgument = new("ci-name") { Arity = ArgumentArity.ZeroOrOne };
        private readonly CommonTracerSettings _tracerSettings;

        public ConfigureCiCommand(ApplicationContext applicationContext)
            : base("configure", "Set the environment variables for the CI")
        {
            _applicationContext = applicationContext;

            _tracerSettings = new(this);
            AddArgument(_nameArgument);

            AddExample("dd-trace ci configure azp");

            this.SetHandler(Execute);
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

        private void Execute(InvocationContext context)
        {
            var profilerEnvironmentVariables = Utils.GetProfilerEnvironmentVariables(
                context,
                _applicationContext.RunnerFolder,
                _applicationContext.Platform,
                _tracerSettings);

            if (profilerEnvironmentVariables == null)
            {
                context.ExitCode = 1;
                return;
            }

            // Enable CI Visibility mode
            profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.Enabled] = "1";

            var name = _nameArgument.GetValue(context);

            if (!TryExtractCiName(name, out var ciName))
            {
                context.ExitCode = 1;
                return;
            }

            Console.WriteLine("Setting up the environment variables.");

            if (!CIConfiguration.SetupCIEnvironmentVariables(profilerEnvironmentVariables, ciName))
            {
                context.ExitCode = 1;
                return;
            }

            context.ExitCode = 0;
        }
    }
}
