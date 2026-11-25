// <copyright file="TestingPlatformRunnerDetector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Tools.Runner;

/// <summary>
/// Detects whether <c>dotnet test</c> runs the Microsoft.Testing.Platform (MTP) command instead of the VSTest one.
/// The MTP command rejects VSTest-only options such as <c>--collect</c> and <c>--test-adapter-path</c>.
/// </summary>
/// <remarks>
/// Mirrors how the .NET SDK selects the command. SDK 11 and later honor a recognized <c>DOTNET_TEST_RUNNER</c> value first, and
/// SDK 10 and later read <c>test.runner</c> from the closest <c>global.json</c> found from the working directory upwards (only the
/// closest one counts, even when it has no <c>test</c> section). Older SDKs always run VSTest. Runner values are compared
/// case-insensitively, as the SDK does. Asking the SDK directly isn't an option: in Microsoft.Testing.Platform mode
/// <c>dotnet test --help</c> builds the projects of the working directory to list the options of the test applications.
/// The SDK version is only read, with <c>dotnet --version</c>, when the environment or <c>global.json</c> select a runner.
/// The JSON reader accepts a trailing comma that makes SDK 11 ignore <c>global.json</c> and run VSTest.
/// </remarks>
internal static class TestingPlatformRunnerDetector
{
    private const string TestingPlatformRunnerName = "Microsoft.Testing.Platform";
    private const string VsTestRunnerName = "VSTest";
    private const string GlobalJsonFileName = "global.json";

    private static readonly TimeSpan SdkVersionTimeout = TimeSpan.FromMinutes(1);

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TestingPlatformRunnerDetector));

    /// <summary>
    /// Gets whether <c>dotnet test</c> runs the Microsoft.Testing.Platform command.
    /// </summary>
    /// <param name="program">The dotnet executable of the command, used to read the SDK version.</param>
    /// <param name="workingDirectory">Working directory of the <c>dotnet test</c> command, where the <c>global.json</c> search starts.</param>
    /// <param name="testRunnerEnvironmentValue">Value of the <c>DOTNET_TEST_RUNNER</c> environment variable, if any.</param>
    /// <returns>True when the Microsoft.Testing.Platform command is selected.</returns>
    public static bool IsDotnetTestUsingTestingPlatform(string program, string? workingDirectory, string? testRunnerEnvironmentValue)
        => IsDotnetTestUsingTestingPlatform(workingDirectory, testRunnerEnvironmentValue, () => GetSdkMajorVersion(program, workingDirectory));

    [TestingAndPrivateOnly]
    internal static bool IsDotnetTestUsingTestingPlatform(string? workingDirectory, string? testRunnerEnvironmentValue, Func<int?> getSdkMajorVersion)
    {
        // Unrecognized values are ignored by the SDK, which then falls back to global.json.
        var environmentRunner = testRunnerEnvironmentValue?.Trim();
        bool? environmentSelectsTestingPlatform = string.Equals(environmentRunner, TestingPlatformRunnerName, StringComparison.OrdinalIgnoreCase) ? true
                                                : string.Equals(environmentRunner, VsTestRunnerName, StringComparison.OrdinalIgnoreCase) ? false
                                                : null;
        var globalJsonSelectsTestingPlatform = string.Equals(GetGlobalJsonTestRunner(workingDirectory), TestingPlatformRunnerName, StringComparison.OrdinalIgnoreCase);
        if (environmentSelectsTestingPlatform != true && !globalJsonSelectsTestingPlatform)
        {
            // Nothing selects Microsoft.Testing.Platform, whatever the SDK version.
            return false;
        }

        // An SDK version that can't be read counts as an older SDK, which keeps the VSTest behavior.
        var sdkMajorVersion = getSdkMajorVersion();
        if (sdkMajorVersion >= 11 && environmentSelectsTestingPlatform is { } selectedByEnvironment)
        {
            return selectedByEnvironment;
        }

        return sdkMajorVersion >= 10 && globalJsonSelectsTestingPlatform;
    }

    [TestingAndPrivateOnly]
    internal static int? GetSdkMajorVersion(string program, string? workingDirectory)
        => GetSdkMajorVersion(program, workingDirectory, SdkVersionTimeout);

    [TestingAndPrivateOnly]
    internal static int? GetSdkMajorVersion(string program, string? workingDirectory, TimeSpan timeout)
    {
        try
        {
            var startInfo = new ProcessStartInfo(program, "--version")
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

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                Log.Warning("RunCiCommand: Unable to start '{Program} --version' to read the .NET SDK version.", program);
                return null;
            }

            // Both streams are drained asynchronously, so the timeout holds even when the process stops writing without exiting.
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit((int)timeout.TotalMilliseconds) || !Task.WaitAll([outputTask, errorTask], timeout))
            {
                TryKill(process);
                Log.Warning<string, TimeSpan>("RunCiCommand: '{Program} --version' didn't finish within {Timeout}, so the .NET SDK version is unknown.", program, timeout);
                return null;
            }

            // The version is the last line, after any first-run message of the SDK.
            var lines = outputTask.Result.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            if (process.ExitCode == 0 && lines.Length > 0 &&
                int.TryParse(lines[lines.Length - 1].Trim().Split('.')[0], NumberStyles.None, CultureInfo.InvariantCulture, out var majorVersion))
            {
                return majorVersion;
            }

            Log.Warning<string, int, string>("RunCiCommand: Unable to read the .NET SDK version from '{Program} --version' (exit code {ExitCode}): {Output}", program, process.ExitCode, outputTask.Result + errorTask.Result);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "RunCiCommand: Unable to read the .NET SDK version from '{Program} --version'.", program);
        }

        return null;
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "RunCiCommand: Unable to stop the '--version' process.");
        }
    }

    private static string? GetGlobalJsonTestRunner(string? workingDirectory)
    {
        for (var directory = workingDirectory; !StringUtil.IsNullOrEmpty(directory); directory = Path.GetDirectoryName(directory))
        {
            var globalJsonPath = Path.Combine(directory, GlobalJsonFileName);
            if (!File.Exists(globalJsonPath))
            {
                continue;
            }

            try
            {
                var runner = (JsonHelper.ParseJObject(File.ReadAllText(globalJsonPath))["test"] as JObject)?["runner"];
                return runner?.Type == JTokenType.String ? runner.Value<string>() : null;
            }
            catch (Exception ex)
            {
                // SDK 11 runs VSTest when global.json can't be read (SDK 10 fails to start).
                Log.Warning(ex, "RunCiCommand: Unable to read the test runner from {GlobalJsonPath}.", globalJsonPath);
                return null;
            }
        }

        return null;
    }
}
