// <copyright file="ProcessInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Datadog.Trace.Tools.Runner.Checks
{
    internal struct ProcessInfo
    {
        public readonly string Name;

        public readonly int Id;

        public readonly string[] Modules;

        public readonly IReadOnlyDictionary<string, string> EnvironmentVariables;

        public ProcessInfo(Process process)
        {
            Name = process.ProcessName;
            Id = process.Id;
            EnvironmentVariables = ProcessEnvironment.ReadVariables(process);

            Modules = process.Modules
                .OfType<ProcessModule>()
                .Select(p => p.FileName)
                .Where(p => p != null)
                .ToArray();
        }

        public static ProcessInfo? GetProcessInfo(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                return new ProcessInfo(process);
            }
            catch (Exception ex)
            {
                Utils.WriteError("Error while trying to fetch process information: " + ex.Message);
                return null;
            }
        }
    }
}
