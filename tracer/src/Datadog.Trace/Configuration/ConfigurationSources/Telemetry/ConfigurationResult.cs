// <copyright file="ConfigurationResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics.CodeAnalysis;

namespace Datadog.Trace.Configuration.ConfigurationSources.Telemetry;

/// <summary>
/// Represents the result of a call to <see cref="IConfigurationSource"/>
/// </summary>
/// <typeparam name="T">The type of the returned value</typeparam>
public readonly record struct ConfigurationResult<T>
{
    private ConfigurationResult(T? result, string? telemetryOverride, ConfigurationLoadResult loadResult)
    {
        Result = result;
        TelemetryOverride = telemetryOverride;
        LoadResult = loadResult;
    }

    /// <summary>
    /// Gets the extracted configuration value, if it was found
    /// </summary>
    public T? Result { get; }

    /// <summary>
    /// Gets the configuration value that represents the <see cref="Result"/> in telemetry,
    /// where <typeparamref name="T"/> cannot be directly stored in telemetry.
    /// </summary>
    public string? TelemetryOverride { get; }

    /// <summary>
    /// Gets a value indicating whether <see cref="Result"/> result passed validation.
    /// If <c>true</c>, implies that <see cref="Result"/> contains a valid value. If
    /// <c>false</c>, see <see cref="LoadResult"/> for more information.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Result))]
    public bool IsValid => LoadResult == ConfigurationLoadResult.Valid;

    /// <summary>
    /// Gets a value indicating whether the configuration loader should defer to a fallback key
    /// </summary>
    public bool ShouldFallBack => LoadResult is ConfigurationLoadResult.NotFound or ConfigurationLoadResult.ParsingError;

    /// <summary>
    /// Gets a value indicating whether the key was present in the configuration source.
    /// Note that this does not say anything about whether the key was successfully parsed, validated, or converted.
    /// </summary>
    public bool IsPresent => LoadResult != ConfigurationLoadResult.NotFound;

    /// <summary>
    /// Gets a value indicating the result of trying to load the configuration
    /// </summary>
    public ConfigurationLoadResult LoadResult { get; }

    /// <summary>
    /// Creates an instance of <see cref="ConfigurationResult{T}" /> with a <see cref="ConfigurationLoadResult.Valid"/> value
    /// </summary>
    /// <param name="result">The value to use</param>
    /// <returns>The <see cref="ConfigurationResult{T}"/></returns>
    public static ConfigurationResult<int> Valid(int result) => new(result, null, ConfigurationLoadResult.Valid);

    /// <summary>
    /// Creates an instance of <see cref="ConfigurationResult{T}" /> with a <see cref="ConfigurationLoadResult.Valid"/> value
    /// </summary>
    /// <param name="result">The value to use</param>
    /// <returns>The <see cref="ConfigurationResult{T}"/></returns>
    public static ConfigurationResult<bool> Valid(bool result) => new(result, null, ConfigurationLoadResult.Valid);

    /// <summary>
    /// Creates an instance of <see cref="ConfigurationResult{T}" /> with a <see cref="ConfigurationLoadResult.Valid"/> value
    /// </summary>
    /// <param name="result">The value to use</param>
    /// <returns>The <see cref="ConfigurationResult{T}"/></returns>
    public static ConfigurationResult<double> Valid(double result) => new(result, null, ConfigurationLoadResult.Valid);

    /// <summary>
    /// Creates an instance of <see cref="ConfigurationResult{T}" /> with a <see cref="ConfigurationLoadResult.Valid"/> value
    /// </summary>
    /// <param name="result">The value to use</param>
    /// <returns>The <see cref="ConfigurationResult{T}"/></returns>
    public static ConfigurationResult<string> Valid(string result) => new(result, null, ConfigurationLoadResult.Valid);

    /// <summary>
    /// Creates an instance of <see cref="ConfigurationResult{T}" /> with a <see cref="ConfigurationLoadResult.Valid"/> value
    /// </summary>
    /// <param name="result">The value to use</param>
    /// <param name="telemetryOverride">The telemetry value to use where the value of <paramref name="result"/> cannot be stored directly in telemetry.
    /// This is required if <typeparamref name="T"/> is not an int/bool/double/string</param>
    /// <returns>The <see cref="ConfigurationResult{T}"/></returns>
    public static ConfigurationResult<T> Valid(T result, string? telemetryOverride)
        => new(result, telemetryOverride, ConfigurationLoadResult.Valid);

    /// <summary>
    /// Creates an instance of <see cref="ConfigurationResult{T}" /> with a <see cref="ConfigurationLoadResult.ValidationFailure"/> value
    /// </summary>
    /// <param name="result">The value that failed validation</param>
    /// <returns>The <see cref="ConfigurationResult{T}"/></returns>
    public static ConfigurationResult<T> Invalid(T result) => new(result, null, ConfigurationLoadResult.ValidationFailure);

    /// <summary>
    /// Creates an instance of <see cref="ConfigurationResult{T}" /> with a <see cref="ConfigurationLoadResult.NotFound"/> value
    /// </summary>
    /// <returns>The <see cref="ConfigurationResult{T}"/></returns>
    public static ConfigurationResult<T> NotFound() => new(default, null, ConfigurationLoadResult.NotFound);

    /// <summary>
    /// Creates an instance of <see cref="ConfigurationResult{T}" /> with a <see cref="ConfigurationLoadResult.ParsingError"/> value
    /// </summary>
    /// <returns>The <see cref="ConfigurationResult{T}"/></returns>
    public static ConfigurationResult<T> ParseFailure() => new(default, null, ConfigurationLoadResult.ParsingError);
}
