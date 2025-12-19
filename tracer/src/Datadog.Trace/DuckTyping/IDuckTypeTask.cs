// <copyright file="IDuckTypeTask.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.DuckTyping;

/// <summary>
/// Duck type task interface
/// </summary>
/// <typeparam name="T">Type of the result</typeparam>
internal interface IDuckTypeTask<out T> : IDuckType
{
    /// <summary>
    /// Gets a value indicating whether if the task is completed
    /// </summary>
    bool IsCompletedSuccessfully { get; }

    /// <summary>
    /// Gets the result of the task
    /// </summary>
    T? Result { get; }

    /// <summary>
    /// Gets the awaiter for the task
    /// </summary>
    /// <returns>Awaiter instance</returns>
    IDuckTypeAwaiter<T> GetAwaiter();
}

/// <summary>
/// Duck type task interface
/// </summary>
internal interface IDuckTypeTask : IDuckType
{
    /// <summary>
    /// Gets a value indicating whether if the task is completed
    /// </summary>
    bool IsCompletedSuccessfully { get; }

    /// <summary>
    /// Gets the awaiter for the task
    /// </summary>
    /// <returns>Awaiter instance</returns>
    IDuckTypeAwaiter GetAwaiter();
}
