// <copyright file="AotRuntimeSelector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Datadog.Trace.Tools.Runner.Aot.Native;

namespace Datadog.Trace.Tools.Runner.Aot;

/// <summary>
/// The DuckType AOT registry generator loads the application's assemblies and evaluates dynamic duck typing with them, like
/// the application does under JIT: it runs on a runtime of the major version the application targets, when one is installed.
/// On another one, the framework differs (an older runtime can't load the application's assemblies, a newer one has other
/// versions of them: .NET 11 has Microsoft.Extensions.*.Abstractions) and so do the duck typing mappings.
/// </summary>
internal static class AotRuntimeSelector
{
    private const string SelectedVariable = "DD_AOT_INSTRUMENT_RUNTIME_SELECTED";

    /// <summary>
    /// Runs the command again on the runtime <see cref="Select"/> chooses, when it isn't this one.
    /// </summary>
    /// <returns>The exit code of the command run on the other runtime, or null to run it on this one.</returns>
    public static int? TryRelaunch(Version targetRuntime)
    {
        if (Environment.GetEnvironmentVariable(SelectedVariable) == "1" ||
            Environment.ProcessPath is not { } dotnet ||
            !string.Equals(Path.GetFileNameWithoutExtension(dotnet), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            // Already selected, or not run by the dotnet host (e.g. the dd-trace executable).
            return null;
        }

        var frameworks = Path.Combine(Path.GetDirectoryName(dotnet)!, "shared", "Microsoft.NETCore.App");
        var installed = Directory.Exists(frameworks) ? Directory.GetDirectories(frameworks).Select(Path.GetFileName).OfType<string>().ToList() : [];
        if (Select(targetRuntime, Environment.Version, installed) is not { } version)
        {
            return null;
        }

        AotLog.Info($"The application targets .NET {targetRuntime.ToString(2)}: instrumenting on the runtime {version}");
        var startInfo = new ProcessStartInfo(dotnet) { UseShellExecute = false };
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add("--fx-version");
        startInfo.ArgumentList.Add(version);
        startInfo.ArgumentList.Add(typeof(AotRuntimeSelector).Assembly.Location);
        foreach (var argument in Environment.GetCommandLineArgs().Skip(1))
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment[SelectedVariable] = "1";
        using var process = Process.Start(startInfo)!;
        process.WaitForExit();
        return process.ExitCode;
    }

    /// <summary>
    /// Selects the installed runtime to instrument on: the latest of the target's major version, otherwise the closest newer
    /// major version (an older one can't load the application's assemblies).
    /// </summary>
    /// <returns>The version (the name of its directory) when it isn't the current runtime's major version; otherwise, null.</returns>
    internal static string? Select(Version targetRuntime, Version currentRuntime, IEnumerable<string> installed)
    {
        var candidates = installed.Select(name => (Name: name, Version: ParseVersion(name)))
                                  .Where(c => c.Version is not null && c.Version.Major >= targetRuntime.Major)
                                  .OrderBy(c => c.Version!.Major)
                                  .ThenByDescending(c => c.Version)
                                  .ThenBy(c => c.Name.Contains('-')) // a release before its previews
                                  .ToList();
        if (candidates.Count == 0 || candidates[0].Version!.Major == currentRuntime.Major)
        {
            return null;
        }

        return candidates[0].Name;
    }

    // 8.0.31, 11.0.0-rc.1.26425.128
    private static Version? ParseVersion(string name)
        => Version.TryParse(name.Split('-')[0], out var version) ? version : null;
}
#endif
