// <copyright file="CommandLineParametersAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Process
{
    internal static class CommandLineParametersAnalyzer
    {
        private static Regex paramDeny = new Regex(@"^-[-]?p|password|passwd|api[_]?key|[access|auth]_token|secret|mysql_pwd|credentials|stripetoken[$]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static string[] allowedEnvVariables = new string[] { "LD_PRELOAD", "LD_LIBRARY_PATH", "PATH" };

        private static bool IsAllowedVariable(string text)
        {
            foreach (var allowedvar in allowedEnvVariables)
            {
                if (text.ToUpper() == allowedvar)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDenied(string param)
        {
            return paramDeny.IsMatch(param);
        }

        internal static string ScrubbingEnvVariables(IDictionary<string, string> envVariables)
        {
            if (envVariables != null)
            {
                string variableLine = string.Empty;

                foreach (var variable in envVariables.Keys)
                {
                    if (IsAllowedVariable(variable))
                    {
                        variableLine += variable + "=" + envVariables[variable];

                        if (variable != envVariables.Keys.Last())
                        {
                            variableLine += "\n";
                        }
                    }
                }
            }

            return null;
        }
    }
}
