// <copyright file="IastMetricsVerbosityLevelExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Iast.Telemetry;

internal static class IastMetricsVerbosityLevelExtensions
{
    public const string Off = "Off";
    public const string Debug = "Debug";
    public const string Information = "Information";
    public const string Mandatory = "Mandatory";
    public const string Unknown = "UNKNOWN";

    public static string GetName(this IastMetricsVerbosityLevel logLevel)
        => logLevel switch
        {
            IastMetricsVerbosityLevel.Off => Off,
            IastMetricsVerbosityLevel.Mandatory => Mandatory,
            IastMetricsVerbosityLevel.Debug => Debug,
            IastMetricsVerbosityLevel.Information => Information,
            _ => Unknown,
        };

    public static IastMetricsVerbosityLevel Parse(string value)
        => value?.ToUpperInvariant() switch
        {
            "OFF" => IastMetricsVerbosityLevel.Off,
            "MANDATORY" => IastMetricsVerbosityLevel.Mandatory,
            "DEBUG" => IastMetricsVerbosityLevel.Debug,
            "INFORMATION" => IastMetricsVerbosityLevel.Information,
            // Default value
            _ => IastMetricsVerbosityLevel.Information
        };
}
