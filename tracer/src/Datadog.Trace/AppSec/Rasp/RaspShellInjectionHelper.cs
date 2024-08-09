// <copyright file="RaspShellInjectionHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

#nullable enable
namespace Datadog.Trace.AppSec.Rasp;

internal static class RaspShellInjectionHelper
{
    private static readonly HashSet<string> KnownShellExecutables =
    [
        // Windows shells
        "cmd.exe",
        "powershell.exe",
        "pwsh.exe",
        // Unix-like shells (macOS & Linux)
        "/bin/sh",
        "/bin/bash",
        "/bin/dash",
        "/bin/zsh",
        "/bin/ksh",
        "/bin/fish",
        "/usr/bin/sh",
        "/usr/bin/bash",
        "/usr/bin/dash",
        "/usr/bin/zsh",
        "/usr/bin/ksh",
        "/usr/bin/fish",
        "/usr/local/bin/sh",
        "/usr/local/bin/bash",
        "/usr/local/bin/zsh",
        "/usr/local/bin/ksh",
        "/sbin/sh",
        "/sbin/bash",
        "/sbin/zsh",
        "/sbin/ksh"
    ];

    internal static bool IsShellInvocation(ProcessStartInfo processStartInfo)
    {
        // Check if UseShellExecute is true
        if (processStartInfo.UseShellExecute)
        {
            return true;
        }

        // Check if the FileName is a known shell executable
        if (processStartInfo.FileName != null &&
            KnownShellExecutables.Contains(processStartInfo.FileName.ToLower()))
        {
            return true;
        }

        return false;
    }

    internal static string BuildCommandInjectionCommand(string file, string argumentLine, Collection<string>? argumentList)
    {
        if (string.IsNullOrEmpty(file))
        {
            return string.Empty;
        }

        if ((argumentList is not null) && (argumentList.Count > 0))
        {
            return file + " " + string.Join(" ", argumentList);
        }

        if (!string.IsNullOrEmpty(argumentLine))
        {
            return file + " " + argumentLine;
        }
        else
        {
            return file;
        }
    }
}
