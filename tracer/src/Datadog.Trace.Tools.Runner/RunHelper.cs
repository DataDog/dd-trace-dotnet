// <copyright file="RunHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal static class RunHelper
    {
        public static IReadOnlyList<string> GetArguments(CommandContext context, RunSettings settings)
        {
            return settings.Command ?? context.Remaining.Raw;
        }

        public static bool TryGetEnvironmentVariables(ApplicationContext applicationContext, RunSettings settings, out Dictionary<string, string> profilerEnvironmentVariables)
        {
            profilerEnvironmentVariables = Utils.GetProfilerEnvironmentVariables(
                applicationContext.RunnerFolder,
                applicationContext.Platform,
                settings);

            if (profilerEnvironmentVariables is null)
            {
                return false;
            }

            if (settings.AdditionalEnvironmentVariables != null)
            {
                foreach (var env in settings.AdditionalEnvironmentVariables)
                {
                    var (key, value) = ParseEnvironmentVariable(env);

                    profilerEnvironmentVariables[key] = value;
                }
            }

            if (Program.CallbackForTests is null)
            {
                Utils.SetCommonTracerSettingsToCurrentProcess(settings);
            }

            return true;
        }

        public static ValidationResult Validate(CommandContext context, RunSettings settings)
        {
            var args = GetArguments(context, settings);
            if (args.Count == 0)
            {
                return ValidationResult.Error("Missing command");
            }

            if (settings.AdditionalEnvironmentVariables != null)
            {
                foreach (var env in settings.AdditionalEnvironmentVariables)
                {
                    if (!env.Contains('='))
                    {
                        return ValidationResult.Error($"Badly formatted environment variable: {env}");
                    }
                }
            }

            return ValidationResult.Success();
        }

        private static (string Key, string Value) ParseEnvironmentVariable(string env)
        {
            var values = env.Split('=', 2);

            return (values[0], values[1]);
        }
    }
}
