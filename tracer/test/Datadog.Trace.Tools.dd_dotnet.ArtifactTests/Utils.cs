// <copyright file="Utils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Runtime.InteropServices;
using Datadog.Trace.TestHelpers;

namespace Datadog.Trace.Tools.dd_dotnet.ArtifactTests;

public static class Utils
{
    internal static string GetApiWrapperPath()
    {
#if NETFRAMEWORK
        return string.Empty;
#else
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return string.Empty;
        }

        string archFolder;

        if (FrameworkDescription.Instance.ProcessArchitecture == ProcessArchitecture.Arm64)
        {
            archFolder = IsAlpine() ? "linux-musl-arm64" : "linux-arm64";
        }
        else
        {
            archFolder = IsAlpine() ? "linux-musl-x64" : "linux-x64";
        }

        return Path.Combine(EnvironmentHelper.GetMonitoringHomePath(), archFolder, "Datadog.Linux.ApiWrapper.x64.so");
#endif
    }

    internal static string GetDdDotnetPath()
    {
        var rid = (EnvironmentTools.GetOS(), EnvironmentTools.GetPlatform(), EnvironmentHelper.IsAlpine()) switch
        {
            ("win", _, _) => "win-x64",
            ("linux", "Arm64", false) => "linux-arm64",
            ("linux", "Arm64", true) => "linux-musl-arm64",
            ("linux", "X64", false) => "linux-x64",
            ("linux", "X64", true) => "linux-musl-x64",
            _ => throw new PlatformNotSupportedException()
        };

        return Path.Combine(EnvironmentHelper.GetMonitoringHomePath(), rid, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dd-dotnet.exe" : "dd-dotnet");
    }

    internal static bool IsAlpine()
    {
        try
        {
            if (File.Exists("/etc/os-release"))
            {
                var strArray = File.ReadAllLines("/etc/os-release");
                foreach (var str in strArray)
                {
                    if (str.StartsWith("ID=", StringComparison.Ordinal))
                    {
                        return str.Substring(3).Trim('"', '\'') == "alpine";
                    }
                }
            }
        }
        catch
        {
            // ignore error checking if the file doesn't exist or we can't read it
        }

        return false;
    }
}
