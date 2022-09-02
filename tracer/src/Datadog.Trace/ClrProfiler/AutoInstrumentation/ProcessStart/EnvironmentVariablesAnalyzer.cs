// <copyright file="EnvironmentVariablesAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Process
{
    internal static class EnvironmentVariablesAnalyzer
    {
        private static string[] allowedEnvVariables = new string[] { "LD_PRELOAD", "LD_LIBRARY_PATH", "PATH" };

        private static bool IsAllowedVariable(string text)
        {
            foreach (var allowedVariable in allowedEnvVariables)
            {
                if (string.Equals(allowedVariable, text, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        internal static string ScrubbingEnvVariables(IDictionary<string, string> envVariables)
        {
            if (envVariables != null)
            {
                var variableLine = new StringBuilder();

                foreach (var variable in envVariables)
                {
                    if (IsAllowedVariable(variable.Key))
                    {
                        variableLine.Append(variable.Key).Append("=").AppendLine(variable.Value);
                    }
                }

                return variableLine.ToString();
            }

            return null;
        }
    }
}
