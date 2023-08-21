// <copyright file="IastMetricsLogLevelExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Iast.Telemetry;

internal static class IastMetricsLogLevelExtensions
{
    public const string Off = "Off";
    public const string Debug = "Debug";
    public const string Information = "Information";
    public const string Mandatory = "Mandatory";
    public const string Unknown = "UNKNOWN";

    public static string GetName(this IastMetricsLogLevel logLevel)
        => logLevel switch
        {
            IastMetricsLogLevel.Off => Off,
            IastMetricsLogLevel.Mandatory => Mandatory,
            IastMetricsLogLevel.Debug => Debug,
            IastMetricsLogLevel.Information => Information,
            _ => Unknown,
        };

    public static IastMetricsLogLevel Parse(string value)
        => value?.ToUpperInvariant() switch
        {
            "OFF" => IastMetricsLogLevel.Off,
            "MANDATORY" => IastMetricsLogLevel.Mandatory,
            "DEBUG" => IastMetricsLogLevel.Debug,
            "INFORMATION" => IastMetricsLogLevel.Information,
            // Default value
            _ => IastMetricsLogLevel.Information
        };
}
