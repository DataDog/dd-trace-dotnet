// <copyright file="RunnerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Datadog.Trace.TestHelpers;

namespace Datadog.Trace.Tools.Runner.ArtifactTests;

public abstract class RunnerTests
{
    protected ProcessHelper StartProcess(string arguments, params (string Key, string Value)[] environmentVariables)
    {
        var targetFolder = GetRunnerToolTargetFolder();
        var executable = Path.Combine(targetFolder, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dd-trace.exe" : "dd-trace");

        var processStart = new ProcessStartInfo(executable, arguments)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        foreach (var (key, value) in environmentVariables)
        {
            processStart.EnvironmentVariables[key] = value;
        }

        return new ProcessHelper(Process.Start(processStart));
    }

    private static string GetRunnerToolTargetFolder()
    {
        var folder = Environment.GetEnvironmentVariable("ToolInstallDirectory");

        if (string.IsNullOrEmpty(folder))
        {
            folder = Path.Combine(
                EnvironmentTools.GetSolutionDirectory(),
                "tracer",
                "bin",
                "runnerTool",
                "installed");
        }

        return folder;
    }
}
