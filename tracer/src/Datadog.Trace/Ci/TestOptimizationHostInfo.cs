// <copyright file="TestOptimizationHostInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci;

internal sealed class TestOptimizationHostInfo : ITestOptimizationHostInfo
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TestOptimizationHostInfo));
    private string? _osVersion;

    public string GetOperatingSystemVersion()
        => _osVersion ??= GetOperatingSystemVersionInternal();

    private string GetOperatingSystemVersionInternal()
    {
        switch (FrameworkDescription.Instance.OSPlatform)
        {
            case OSPlatformName.Linux:
                if (!string.IsNullOrEmpty(HostMetadata.Instance.KernelRelease))
                {
                    return HostMetadata.Instance.KernelRelease!;
                }

                break;
            case OSPlatformName.MacOS:
                try
                {
                    // Executes the command "uname -r" to fetch the macOS version.
                    var osxVersionCommand = ProcessHelpers.RunCommand(new ProcessHelpers.Command("uname", "-r"));
                    var osxVersion = osxVersionCommand?.Output.Trim(' ', '\n');
                    if (!string.IsNullOrEmpty(osxVersion))
                    {
                        return osxVersion!;
                    }
                }
                catch (Exception ex)
                {
                    // Log the exception if retrieving the macOS version fails.
                    Log.Warning(ex, "TestOptimizationHostInfo: Error getting OS version on macOS");
                }

                break;
        }

        // Fallback to the default OS version string.
        return Environment.OSVersion.VersionString;
    }
}
