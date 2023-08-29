// <copyright file="IastMetricsVerbosityLevelExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Iast.Telemetry;

internal static class IastMetricsVerbosityLevelExtensions
{
    public const string Off = "off";
    public const string Debug = "debug";
    public const string Information = "information";
    public const string Mandatory = "mandatory";

    public static IastMetricsVerbosityLevel Parse(string value)
        => value?.ToLowerInvariant() switch
        {
            Off => IastMetricsVerbosityLevel.Off,
            Mandatory => IastMetricsVerbosityLevel.Mandatory,
            Debug => IastMetricsVerbosityLevel.Debug,
            Information => IastMetricsVerbosityLevel.Information,
            // Default value
            _ => IastMetricsVerbosityLevel.Information
        };
}
