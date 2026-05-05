// <copyright file="IDuckTypeValueTask.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.DuckTyping;

/// <summary>
/// Duck type value task interface
/// </summary>
/// <typeparam name="T">Type of the result</typeparam>
public interface IDuckTypeValueTask<out T> : IDuckType
{
    /// <summary>
    /// Gets a value indicating whether the value task completed successfully
    /// </summary>
    bool IsCompletedSuccessfully { get; }

    /// <summary>
    /// Gets the result of the value task
    /// </summary>
    T? Result { get; }

    /// <summary>
    /// Gets the awaiter for the value task
    /// </summary>
    /// <returns>Awaiter instance</returns>
    IDuckTypeAwaiter<T> GetAwaiter();
}

/// <summary>
/// Duck type value task interface
/// </summary>
public interface IDuckTypeValueTask : IDuckType
{
    /// <summary>
    /// Gets a value indicating whether the value task completed successfully
    /// </summary>
    bool IsCompletedSuccessfully { get; }

    /// <summary>
    /// Gets the awaiter for the value task
    /// </summary>
    /// <returns>Awaiter instance</returns>
    IDuckTypeAwaiter GetAwaiter();
}
