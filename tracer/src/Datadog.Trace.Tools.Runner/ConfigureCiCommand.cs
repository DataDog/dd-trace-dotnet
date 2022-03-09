// <copyright file="ConfigureCiCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal class ConfigureCiCommand : Command<ConfigureCiSettings>
    {
        private static readonly IReadOnlyDictionary<string, CIName> CiNamesMapping
            = new Dictionary<string, CIName>(StringComparer.OrdinalIgnoreCase)
        {
            ["azp"] = CIName.AzurePipelines
        };

        public ConfigureCiCommand(ApplicationContext applicationContext)
        {
            ApplicationContext = applicationContext;
        }

        protected ApplicationContext ApplicationContext { get; }

        public override int Execute(CommandContext context, ConfigureCiSettings settings)
        {
            var profilerEnvironmentVariables = Utils.GetProfilerEnvironmentVariables(
                ApplicationContext.RunnerFolder,
                ApplicationContext.Platform,
                settings);

            if (profilerEnvironmentVariables == null)
            {
                return 1;
            }

            // Enable CI Visibility mode
            profilerEnvironmentVariables[Configuration.ConfigurationKeys.CIVisibility.Enabled] = "1";

            if (!TryExtractCiName(settings, out var ciName))
            {
                return 1;
            }

            AnsiConsole.WriteLine("Setting up the environment variables.");

            if (!CIConfiguration.SetupCIEnvironmentVariables(profilerEnvironmentVariables, ciName))
            {
                return 1;
            }

            return 0;
        }

        private static bool TryExtractCiName(ConfigureCiSettings settings, out CIName? ciName)
        {
            ciName = null;

            if (string.IsNullOrEmpty(settings.CiName))
            {
                return true;
            }

            if (CiNamesMapping.TryGetValue(settings.CiName, out var mappedCiName))
            {
                ciName = mappedCiName;
                return true;
            }

            Utils.WriteError($"Unsupported CI name: {settings.CiName}. The supported values are: {string.Join(", ", CiNamesMapping.Keys)}.");
            return false;
        }
    }
}
