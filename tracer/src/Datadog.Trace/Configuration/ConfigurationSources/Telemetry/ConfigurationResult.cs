// <copyright file="ConfigurationResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics.CodeAnalysis;

namespace Datadog.Trace.Configuration.ConfigurationSources.Telemetry;

internal readonly record struct ConfigurationResult<T>
{
    private ConfigurationResult(T? result, ConfigurationLoadResult loadResult)
    {
        Result = result;
        LoadResult = loadResult;
    }

    /// <summary>
    /// Gets the extracted configuration value, if it was found
    /// </summary>
    public T? Result { get; }

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

    public static ConfigurationResult<T> Valid(T result) => new(result, ConfigurationLoadResult.Valid);

    public static ConfigurationResult<T> Invalid(T result) => new(result, ConfigurationLoadResult.ValidationFailure);

    public static ConfigurationResult<T> NotFound() => new(default, ConfigurationLoadResult.NotFound);

    public static ConfigurationResult<T> ParseFailure() => new(default, ConfigurationLoadResult.ParsingError);
}
