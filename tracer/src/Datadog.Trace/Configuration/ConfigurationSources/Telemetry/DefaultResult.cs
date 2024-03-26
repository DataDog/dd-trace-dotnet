// <copyright file="DefaultResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Configuration.ConfigurationSources.Telemetry;

internal readonly record struct DefaultResult<T>
{
    public DefaultResult(T result, string? telemetryValue)
    {
        Result = result;
        TelemetryValue = telemetryValue ?? result?.ToString();
    }

    /// <summary>
    /// Gets the value to use as the default result
    /// </summary>
    public T Result { get; }

    /// <summary>
    /// Gets a string representation of the result to use in telemetry.
    /// </summary>
    public string? TelemetryValue { get; }

    public static implicit operator DefaultResult<T>(T result) => new(result, telemetryValue: null);
}
