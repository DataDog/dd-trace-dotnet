// <copyright file="EnvironmentVariablesScrubber.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Process
{
    internal static class EnvironmentVariablesScrubber
    {
        private static string[] allowedEnvironmentVariables = new string[] { "LD_PRELOAD", "LD_LIBRARY_PATH", "PATH" };

        private static bool IsAllowedVariable(string text)
        {
            foreach (var allowedVariable in allowedEnvironmentVariables)
            {
                if (string.Equals(allowedVariable, text, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        internal static string ScrubEnvironmentVariables(IDictionary<string, string> envVariables)
        {
            if (envVariables != null)
            {
                var variableLine = StringBuilderCache.Acquire(0);

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
