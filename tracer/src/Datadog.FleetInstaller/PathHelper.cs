// <copyright file="PathHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Reflection;

namespace Datadog.FleetInstaller;

internal static class PathHelper
{
    private const string ForwarderFileName = "telemetry_forwarder.exe";

    public static string GetTelemetryForwarderPath()
    {
        // Get path of FleetInstaller.exe
        var fleetInstallerPath = Assembly.GetExecutingAssembly().Location;
        if (string.IsNullOrEmpty(fleetInstallerPath))
        {
            // Shouldn't ever be needed, but let's play it safe
            fleetInstallerPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        }

        if (string.IsNullOrEmpty(fleetInstallerPath)
            || Path.GetDirectoryName(fleetInstallerPath) is not { } directory)
        {
            // I guess we failed for some reason, so we can't calculate the path to the telemetry forwarder
            return string.Empty;
        }

        // Ok, now we have the path
        return Path.Combine(directory, ForwarderFileName);
    }
}
