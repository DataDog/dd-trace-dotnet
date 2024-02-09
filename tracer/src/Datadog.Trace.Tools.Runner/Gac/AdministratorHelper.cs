// <copyright file="AdministratorHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace Datadog.Trace.Tools.Runner.Gac;

#if NET5_0_OR_GREATER
[SupportedOSPlatform("windows")]
#endif
internal static class AdministratorHelper
{
    private static bool? _isElevated;

    public static bool IsElevated
    {
        get
        {
            _isElevated ??= new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            return _isElevated.Value;
        }
    }

    public static void EnsureIsElevated()
    {
        if (!IsElevated)
        {
            var commandLineArguments = Environment.GetCommandLineArgs();
#if NET6_0_OR_GREATER
            var processPath = Environment.ProcessPath ?? commandLineArguments[0];
#else
            var processPath = commandLineArguments[0];
#endif

            var processInfo = new ProcessStartInfo(processPath)
            {
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true
            };

            foreach (var arg in commandLineArguments.Skip(1))
            {
                processInfo.ArgumentList.Add(arg);
            }

            var process = Process.Start(processInfo);
            if (process is null)
            {
                Console.WriteLine("Process cannot be executed.");
                Environment.Exit(1);
            }
            else
            {
                process.WaitForExit();
                Console.WriteLine("Returned: {0}", process.ExitCode);
                Environment.Exit(process.ExitCode);
            }
        }
    }
}
