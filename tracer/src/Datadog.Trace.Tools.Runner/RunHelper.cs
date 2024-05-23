// <copyright file="RunHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.CommandLine.Invocation;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Tools.Runner
{
    internal static class RunHelper
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RunHelper));

        public static bool TryGetEnvironmentVariables(ApplicationContext applicationContext, InvocationContext invocationContext, CommonTracerSettings settings, out Dictionary<string, string> profilerEnvironmentVariables)
        {
            return TryGetEnvironmentVariables(applicationContext, invocationContext, settings, Utils.CIVisibilityOptions.None, out profilerEnvironmentVariables);
        }

        public static bool TryGetEnvironmentVariables(ApplicationContext applicationContext, InvocationContext invocationContext, CommonTracerSettings settings, Utils.CIVisibilityOptions ciVisibilityOptions, out Dictionary<string, string> profilerEnvironmentVariables)
        {
            profilerEnvironmentVariables = Utils.GetProfilerEnvironmentVariables(
                invocationContext,
                applicationContext.RunnerFolder,
                applicationContext.Platform,
                settings,
                ciVisibilityOptions: ciVisibilityOptions);

            if (profilerEnvironmentVariables is null)
            {
                return false;
            }

            if (settings is RunSettings runSettings)
            {
                var additionalEnvironmentVariables = invocationContext.ParseResult.GetValueForOption(runSettings.AdditionalEnvironmentVariables);

                if (additionalEnvironmentVariables != null)
                {
                    foreach (var env in additionalEnvironmentVariables)
                    {
                        var (key, value) = ParseEnvironmentVariable(env);

                        profilerEnvironmentVariables[key] = value;
                        if (Program.CallbackForTests is null)
                        {
                            Log.Debug("Setting: {Key}={Value}", key, value);
                            EnvironmentHelpers.SetEnvironmentVariable(key, value);
                        }
                    }
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
