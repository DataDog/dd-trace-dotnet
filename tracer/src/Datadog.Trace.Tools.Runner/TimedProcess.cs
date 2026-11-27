// <copyright file="TimedProcess.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Tools.Runner;

/// <summary>
/// Runs short helper processes of the runner, such as <c>dotnet --version</c> or <c>dotnet msbuild</c>, with a timeout that is enforced.
/// </summary>
internal static class TimedProcess
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TimedProcess));

    /// <summary>
    /// Runs a process and returns its output, stopping it when it doesn't finish within the timeout.
    /// </summary>
    /// <remarks>
    /// <c>ProcessHelpers.RunCommand</c> isn't used because it blocks reading the output before it checks its timeout. Both
    /// streams are drained asynchronously instead, which is fine in the runner: it only runs on .NET Core, without AppDomains.
    /// This class is temporary: once https://github.com/DataDog/dd-trace-dotnet/pull/9841 enforces that timeout, the callers
    /// switch to <c>ProcessHelpers.RunCommand</c> and this class is removed.
    /// </remarks>
    public static bool TryRun(string fileName, string arguments, string? workingDirectory, TimeSpan timeout, out string output, out string error, out int exitCode)
    {
        output = string.Empty;
        error = string.Empty;
        exitCode = -1;
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (workingDirectory is not null)
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        var stopwatch = Stopwatch.StartNew();
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"'{fileName}' couldn't be started.");

        var finished = false;
        try
        {
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(GetRemainingMilliseconds(timeout, stopwatch)) ||
                !Task.WaitAll([outputTask, errorTask], GetRemainingMilliseconds(timeout, stopwatch)))
            {
                return false;
            }

            output = outputTask.Result;
            error = errorTask.Result;
            exitCode = process.ExitCode;
            finished = true;
            return true;
        }
        finally
        {
            if (!finished)
            {
                Kill(process, fileName);
            }
        }
    }

    private static int GetRemainingMilliseconds(TimeSpan timeout, Stopwatch stopwatch)
        => timeout > stopwatch.Elapsed ? (int)(timeout - stopwatch.Elapsed).TotalMilliseconds : 0;

    private static void Kill(Process process, string fileName)
    {
        try
        {
#if NETCOREAPP3_0_OR_GREATER
            // dotnet msbuild starts child processes (build nodes) that would keep running.
            process.Kill(entireProcessTree: true);
#else
            process.Kill();
#endif
        }
        catch (Exception ex)
        {
            // The process can exit on its own before the kill.
            Log.Debug(ex, "TimedProcess: Unable to stop '{FileName}'.", fileName);
        }
    }
}
