// <copyright file="NgenHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETFRAMEWORK

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers;

public static class NgenHelper
{
    public static void InstallToNativeImageCache(ITestOutputHelper output, string applicationPath)
    {
        var appFilename = Path.GetFileName(applicationPath);
        var workingDirectory = Path.GetDirectoryName(applicationPath);

        var install = $"install \"{appFilename}\"";
        RunNgen(output, workingDirectory, install);
    }

    public static void UninstallFromNativeImageCache(ITestOutputHelper output, string applicationPath)
    {
        var appFilename = Path.GetFileName(applicationPath);
        var workingDirectory = Path.GetDirectoryName(applicationPath);

        var install = $"uninstall \"{appFilename}\"";
        RunNgen(output, workingDirectory, install);
    }

    private static void RunNgen(ITestOutputHelper output, string workingDirectory, string arguments)
    {
        var frameworkDirectory = RuntimeEnvironment.GetRuntimeDirectory();
        var ngenPath = Path.Combine(frameworkDirectory, "ngen.exe");
        var startInfo = new ProcessStartInfo(ngenPath, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            WorkingDirectory = workingDirectory
        };

        output.WriteLine($"Running {ngenPath} {arguments}");
        var process = Process.Start(startInfo);

        using var helper = new ProcessHelper(process);
        var timeoutMs = 60_000;

        var ranToCompletion = process.WaitForExit(timeoutMs) && helper.Drain(timeoutMs / 2);
        var standardOutput = helper.StandardOutput;
        var standardError = helper.ErrorOutput;

        if (!ranToCompletion)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    // Do nothing
                }
            }

            output.WriteLine("NGEN was running for too long or was lost.");
            output.WriteLine($"StandardOutput:{Environment.NewLine}{standardOutput}");
            output.WriteLine($"StandardError:{Environment.NewLine}{standardError}");

            throw new TimeoutException("The smoke test is running for too long or was lost.");
        }

        if (!string.IsNullOrWhiteSpace(standardOutput))
        {
            output.WriteLine($"StandardOutput:{Environment.NewLine}{standardOutput}");
        }

        if (!string.IsNullOrWhiteSpace(standardError))
        {
            output.WriteLine($"StandardError:{Environment.NewLine}{standardError}");
        }

        if (process.ExitCode != 0)
        {
            throw new Exception($"Error executing {ngenPath} {arguments} - are you running with admin priviliges?");
        }

        output.WriteLine("NGEN command completed successfully.");
    }
}
#endif
