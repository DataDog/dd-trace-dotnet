// <copyright file="EnvironmentVariablesAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Process
{
    internal static class EnvironmentVariablesAnalyzer
    {
        private static string[] allowedEnvVariables = new string[] { "LD_PRELOAD", "LD_LIBRARY_PATH", "PATH" };

        private static bool IsAllowedVariable(string text)
        {
            return allowedEnvVariables.Contains(text.ToUpper());
        }

        internal static string ScrubbingEnvVariables(IDictionary<string, string> envVariables)
        {
            if (envVariables != null)
            {
                var variableLine = string.Empty;

                foreach (var variable in envVariables.Keys)
                {
                    if (IsAllowedVariable(variable))
                    {
                        variableLine += variable + "=" + envVariables[variable] + Environment.NewLine;
                    }
                }

                return variableLine;
            }

            return null;
        }
    }
}
