// <copyright file="PathHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;

namespace Datadog.FleetInstaller;

internal static class PathHelper
{
    private const string ForwarderFileName = "telemetry_forwarder.exe";

    public static string GetTelemetryForwarderPath(string homePath)
    {
        if (string.IsNullOrEmpty(homePath)
            || !Path.IsPathRooted(homePath) // can't use relative paths
            || Path.GetDirectoryName(homePath) is not { } directory)
        {
            // I guess we failed for some reason, so we can't calculate the path to the telemetry forwarder
            return string.Empty;
        }

        // Ok, now we have the path
        // Should we care about long paths?
        return Path.Combine(Path.GetFullPath(directory), "installer", ForwarderFileName);
    }
}
