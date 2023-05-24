// <copyright file="ConfigurationResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration.ConfigurationSources.Telemetry;

internal readonly record struct ConfigurationResult<T>
{
    private ConfigurationResult(T result, bool isValid)
    {
        Result = result;
        IsValid = isValid;
    }

    /// <summary>
    /// Gets the extracted configuration value.
    /// </summary>
    public T Result { get; }

    /// <summary>
    /// Gets a value indicating whether <see cref="Result"/> result passed validation.
    /// </summary>
    public bool IsValid { get; }

    public static ConfigurationResult<T> Valid(T result) => new(result, isValid: true);

    public static ConfigurationResult<T> Invalid(T result) => new(result, isValid: false);
}
