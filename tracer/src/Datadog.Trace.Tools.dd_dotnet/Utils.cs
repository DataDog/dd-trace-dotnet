// <copyright file="Utils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Spectre.Console;

namespace Datadog.Trace.Tools.dd_dotnet;

internal class Utils
{
    public const string Profilerid = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";

    public static bool IsAlpine()
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

    internal static void WriteException(Exception exception)
    {
        AnsiConsole.Write(new Markup($"[red]Error:[/] {Markup.Escape(exception.ToString())}{Environment.NewLine}"));
    }

    internal static void WriteError(string message)
    {
        AnsiConsole.MarkupLine($"[red] [[FAILURE]]: {message.EscapeMarkup()}[/]");
    }

    internal static void WriteWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow] [[WARNING]]: {message.EscapeMarkup()}[/]");
    }

    internal static void WriteSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[lime] [[SUCCESS]]: {message.EscapeMarkup()}[/]");
    }

    internal static void WriteInfo(string message)
    {
        AnsiConsole.MarkupLine($"[aqua] [[INFO]]: {message.EscapeMarkup()}[/]");
    }

    internal static Dictionary<string, string>? GetBaseProfilerEnvironmentVariables(string? customTracerHome)
    {
        string? tracerHome = null;

        if (!string.IsNullOrEmpty(customTracerHome))
        {
            tracerHome = Path.GetFullPath(customTracerHome);

            if (!Directory.Exists(tracerHome))
            {
                WriteError("Error: The specified home folder doesn't exist.");
            }
        }

        tracerHome ??= Path.Combine(AppContext.BaseDirectory, "..");

        string tracerMsBuild = FileExists(Path.Combine(tracerHome, "netstandard2.0", "Datadog.Trace.MSBuild.dll"));
        string tracerProfiler32 = string.Empty;
        string tracerProfiler64 = string.Empty;
        string? tracerProfilerArm64 = null;
        string ldPreload = string.Empty;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            tracerProfiler32 = FileExists(Path.Combine(tracerHome, "win-x86", "Datadog.Trace.ClrProfiler.Native.dll"));
            tracerProfiler64 = FileExists(Path.Combine(tracerHome, "win-x64", "Datadog.Trace.ClrProfiler.Native.dll"));
            tracerProfilerArm64 = FileExistsOrNull(Path.Combine(tracerHome, "win-ARM64EC", "Datadog.Trace.ClrProfiler.Native.dll"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (RuntimeInformation.OSArchitecture == Architecture.X64)
            {
                var archFolder = IsAlpine() ? "linux-musl-x64" : "linux-x64";
                tracerProfiler64 = FileExists(Path.Combine(tracerHome, archFolder, "Datadog.Trace.ClrProfiler.Native.so"));
                ldPreload = FileExists(Path.Combine(tracerHome, archFolder, "Datadog.Linux.ApiWrapper.x64.so"));
            }
            else if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
            {
                var archFolder = IsAlpine() ? "linux-musl-arm64" : "linux-arm64";
                tracerProfiler64 = FileExists(Path.Combine(tracerHome, archFolder, "Datadog.Trace.ClrProfiler.Native.so"));
                tracerProfilerArm64 = tracerProfiler64;
                ldPreload = FileExists(Path.Combine(tracerHome, archFolder, "Datadog.Linux.ApiWrapper.x64.so"));
            }
            else
            {
                WriteError($"Error: Linux {RuntimeInformation.OSArchitecture} architecture is not supported.");
                return null;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            tracerProfiler64 = FileExists(Path.Combine(tracerHome, "osx", "Datadog.Trace.ClrProfiler.Native.dylib"));
            tracerProfilerArm64 = tracerProfiler64;
        }

        var envVars = new Dictionary<string, string>
        {
            ["DD_DOTNET_TRACER_HOME"] = tracerHome,
            ["DD_DOTNET_TRACER_MSBUILD"] = tracerMsBuild,
            ["CORECLR_ENABLE_PROFILING"] = "1",
            ["CORECLR_PROFILER"] = Profilerid,
            ["CORECLR_PROFILER_PATH_32"] = tracerProfiler32,
            ["CORECLR_PROFILER_PATH_64"] = tracerProfiler64,
            ["COR_ENABLE_PROFILING"] = "1",
            ["COR_PROFILER"] = Profilerid,
            ["COR_PROFILER_PATH_32"] = tracerProfiler32,
            ["COR_PROFILER_PATH_64"] = tracerProfiler64,
            // Preventively set EnableDiagnostics to override any ambient value
            ["COMPlus_EnableDiagnostics"] = "1",
            ["DOTNET_EnableDiagnostics"] = "1",
            ["DOTNET_EnableDiagnostics_Profiler"] = "1",
            ["COMPlus_EnableDiagnostics_Profiler"] = "1",
        };

        if (!string.IsNullOrEmpty(ldPreload))
        {
            envVars["LD_PRELOAD"] = ldPreload;
        }

        if (!string.IsNullOrEmpty(tracerProfilerArm64))
        {
            envVars["CORECLR_PROFILER_PATH_ARM64"] = tracerProfilerArm64;
            envVars["COR_PROFILER_PATH_ARM64"] = tracerProfilerArm64;
        }

        return envVars;
    }

    private static string FileExists(string filePath)
    {
        try
        {
            filePath = Path.GetFullPath(filePath);

            if (!File.Exists(filePath))
            {
                WriteError($"Error: The file '{filePath}' can't be found.");
            }
        }
        catch (Exception ex)
        {
            WriteError($"Error: The file '{filePath}' check thrown an exception: {ex}");
        }

        return filePath;
    }

    private static string? FileExistsOrNull(string filePath)
    {
        try
        {
            filePath = Path.GetFullPath(filePath);

            if (!File.Exists(filePath))
            {
                return null;
            }
        }
        catch (Exception ex)
        {
            WriteError($"Error: The file '{filePath}' check thrown an exception: {ex}");
        }

        return filePath;
    }
}
