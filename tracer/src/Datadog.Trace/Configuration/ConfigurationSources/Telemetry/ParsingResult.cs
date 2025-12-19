// <copyright file="ParsingResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics.CodeAnalysis;

namespace Datadog.Trace.Configuration.ConfigurationSources.Telemetry;

/// <summary>
/// The result of parsing a configuration value
/// </summary>
/// <typeparam name="T"></typeparam>
internal readonly record struct ParsingResult<T>
{
    private ParsingResult(T? result, bool isValid)
    {
        Result = result;
        IsValid = isValid;
    }

    /// <summary>
    /// Gets the extracted configuration value, if parsing was successful
    /// </summary>
    public T? Result { get; }

    /// <summary>
    /// Gets a value indicating whether parsing was successful, and so whether <see cref="Result"/> contains a valid value
    /// </summary>
    [MemberNotNullWhen(true, nameof(Result))]
    public bool IsValid { get; }

    /// <summary>
    /// Implicitly converts a value to a successful <see cref="ParsingResult{T}"/>
    /// </summary>
    /// <param name="result">The value to convert</param>
    /// <returns>The converted value</returns>
    public static implicit operator ParsingResult<T>(T result) => Success(result);

    /// <summary>
    /// Creates an instance of <see cref="ParsingResult{T}"/> representing a successful parsing operation
    /// </summary>
    /// <param name="result">The value to use as the result</param>
    /// <returns>The result</returns>
    public static ParsingResult<T> Success(T result) => new(result, isValid: true);

    /// <summary>
    /// Creates an instance of <see cref="ParsingResult{T}"/> representing a failed parsing operation
    /// </summary>
    /// <returns>The result</returns>
    public static ParsingResult<T> Failure() => new(default, isValid: false);
}
