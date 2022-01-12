// <copyright file="ProcessEnvironment.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Checks
{
    internal class ProcessEnvironment
    {
        public static IReadOnlyDictionary<string, string> ReadVariables(Process process)
        {
            if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                return Windows.ProcessEnvironmentWindows.ReadVariables(process);
            }

            if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                return Linux.ProcessEnvironmentLinux.ReadVariables(process);
            }

            throw new NotSupportedException("Reading environment variables is currently only supported on Windows and Linux.");
        }
    }
}
