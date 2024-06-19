// <copyright file="DefaultResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;

namespace Datadog.Trace.Configuration.ConfigurationSources.Telemetry;

internal readonly record struct DefaultResult<T>
{
    private readonly string? _telemetryValue;

    public DefaultResult(T result, string? telemetryValue)
    {
        Result = result;
        _telemetryValue = telemetryValue;
    }

    /// <summary>
    /// Gets the value to use as the default result
    /// </summary>
    public T Result { get; }

    /// <summary>
    /// Gets a string representation of the result to use in telemetry.
    /// </summary>
    public string? TelemetryValue => _telemetryValue ?? Result?.ToString();

    public static implicit operator DefaultResult<T>(T result)
        => result is IDictionary<string, string>
               ? new(result, telemetryValue: string.Empty) // we don't want to call ToString() on these
               : new(result, null);
}
