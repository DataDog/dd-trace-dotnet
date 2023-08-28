// <copyright file="RunHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.CommandLine.Invocation;

namespace Datadog.Trace.Tools.Runner
{
    internal static class RunHelper
    {
        public static bool TryGetEnvironmentVariables(ApplicationContext applicationContext, InvocationContext invocationContext, RunSettings settings, out Dictionary<string, string> profilerEnvironmentVariables)
        {
            profilerEnvironmentVariables = Utils.GetProfilerEnvironmentVariables(
                invocationContext,
                applicationContext.RunnerFolder,
                applicationContext.Platform,
                settings,
                false);

            if (profilerEnvironmentVariables is null)
            {
                return false;
            }

            var additionalEnvironmentVariables = invocationContext.ParseResult.GetValueForOption(settings.AdditionalEnvironmentVariables);

            if (additionalEnvironmentVariables != null)
            {
                foreach (var env in additionalEnvironmentVariables)
                {
                    var (key, value) = ParseEnvironmentVariable(env);

                    profilerEnvironmentVariables[key] = value;
                }
            }

            if (Program.CallbackForTests is null)
            {
                Utils.SetCommonTracerSettingsToCurrentProcess(invocationContext, settings);
            }

            return true;
        }

        private static (string Key, string Value) ParseEnvironmentVariable(string env)
        {
            var values = env.Split('=', 2);

            return (values[0], values[1]);
        }
    }
}
