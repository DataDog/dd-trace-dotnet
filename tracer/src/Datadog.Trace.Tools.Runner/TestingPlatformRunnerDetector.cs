// <copyright file="TestingPlatformRunnerDetector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Tools.Runner;

/// <summary>
/// Detects whether <c>dotnet test</c> runs the Microsoft.Testing.Platform (MTP) command instead of the VSTest one.
/// The MTP command rejects VSTest-only options such as <c>--collect</c> and <c>--test-adapter-path</c>.
/// </summary>
/// <remarks>
/// Mirrors how the .NET SDK selects the command: a recognized <c>DOTNET_TEST_RUNNER</c> value wins (SDK 11 and later),
/// otherwise <c>test.runner</c> in the closest <c>global.json</c> found from the working directory upwards decides.
/// The values are compared case-insensitively, and only the closest <c>global.json</c> counts, even when it has no
/// <c>test</c> section. SDK 10 ignores <c>DOTNET_TEST_RUNNER</c>; the SDK version isn't known here, so the variable is
/// always honored.
/// </remarks>
internal static class TestingPlatformRunnerDetector
{
    private const string TestingPlatformRunnerName = "Microsoft.Testing.Platform";
    private const string VsTestRunnerName = "VSTest";
    private const string GlobalJsonFileName = "global.json";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TestingPlatformRunnerDetector));

    /// <summary>
    /// Gets whether <c>dotnet test</c> runs the Microsoft.Testing.Platform command.
    /// </summary>
    /// <param name="workingDirectory">Working directory of the <c>dotnet test</c> command, where the <c>global.json</c> search starts.</param>
    /// <param name="testRunnerEnvironmentValue">Value of the <c>DOTNET_TEST_RUNNER</c> environment variable, if any.</param>
    /// <returns>True when the Microsoft.Testing.Platform command is selected.</returns>
    public static bool IsDotnetTestUsingTestingPlatform(string? workingDirectory, string? testRunnerEnvironmentValue)
    {
        // Unrecognized values are ignored by the SDK, which then falls back to global.json.
        var environmentRunner = testRunnerEnvironmentValue?.Trim();
        if (string.Equals(environmentRunner, TestingPlatformRunnerName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(environmentRunner, VsTestRunnerName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(GetGlobalJsonTestRunner(workingDirectory), TestingPlatformRunnerName, StringComparison.OrdinalIgnoreCase);
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
