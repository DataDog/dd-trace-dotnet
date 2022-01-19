// <copyright file="ProcessEnvironmentLinux.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Datadog.Trace.Tools.Runner.Checks.Linux
{
    internal class ProcessEnvironmentLinux
    {
        public static IReadOnlyDictionary<string, string> ReadVariables(Process process)
        {
            /*
                   /proc/[pid]/environ
                          This file contains the environment for the process. The entries are separated by
                          null bytes ('\0'), and there may be a null byte at the end.
            */

            var path = $"/proc/{process.Id}/environ";

            var result = new Dictionary<string, string>();

            foreach (var line in File.ReadAllText(path).Split('\0', System.StringSplitOptions.RemoveEmptyEntries))
            {
                var values = line.Split('=', 2);
                result[values[0]] = values.Length > 1 ? values[1] : string.Empty;
            }

            return result;
        }
    }
}
