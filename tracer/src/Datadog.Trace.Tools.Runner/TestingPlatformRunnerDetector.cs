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
/// Detects whether <c>dotnet test</c> runs the Microsoft.Testing.Platform (MTP) command instead of the VSTest one, the same way the SDK
/// does: <c>DOTNET_TEST_RUNNER</c> first (SDK 11 and later only), then <c>test.runner</c> in the closest <c>global.json</c>.
/// </summary>
internal static class TestingPlatformRunnerDetector
{
    private const string TestingPlatformRunnerName = "Microsoft.Testing.Platform";
    private const string VsTestRunnerName = "VSTest";
    private const string GlobalJsonFileName = "global.json";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TestingPlatformRunnerDetector));

    /// <summary>
    /// Gets whether <c>dotnet test</c> runs the Microsoft.Testing.Platform command for the given inputs.
    /// </summary>
    /// <param name="startDirectory">Directory where the <c>global.json</c> search starts, normally the current directory of the command.</param>
    /// <param name="runnerEnvironmentValue">Value of the <c>DOTNET_TEST_RUNNER</c> environment variable, if any.</param>
    /// <returns>True when the Microsoft.Testing.Platform command is selected.</returns>
    public static bool IsDotnetTestUsingTestingPlatform(string? startDirectory, string? runnerEnvironmentValue)
    {
        // A recognized environment value wins over global.json; unrecognized values fall through, like the SDK does.
        var environmentRunner = runnerEnvironmentValue?.Trim();
        if (string.Equals(environmentRunner, TestingPlatformRunnerName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(environmentRunner, VsTestRunnerName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(GetGlobalJsonRunner(startDirectory), TestingPlatformRunnerName, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetGlobalJsonRunner(string? startDirectory)
    {
        for (var directory = startDirectory; !StringUtil.IsNullOrEmpty(directory); directory = Path.GetDirectoryName(directory))
        {
            var globalJsonPath = Path.Combine(directory, GlobalJsonFileName);
            if (!File.Exists(globalJsonPath))
            {
                continue;
            }

            // Only the closest global.json counts, even when it doesn't configure a runner.
            try
            {
                var globalJson = JsonHelper.ParseJObject(File.ReadAllText(globalJsonPath));
                var runner = (globalJson["test"] as JObject)?["runner"];
                return runner?.Type == JTokenType.String ? runner.Value<string>() : null;
            }
            catch (Exception ex)
            {
                // A malformed global.json makes the SDK fall back to VSTest.
                Log.Debug(ex, "Unable to read the test runner from {GlobalJsonPath}", globalJsonPath);
                return null;
            }
        }

        return null;
    }
}
