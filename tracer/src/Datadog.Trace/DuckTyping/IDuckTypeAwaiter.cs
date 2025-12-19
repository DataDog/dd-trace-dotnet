// <copyright file="IDuckTypeAwaiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Runtime.CompilerServices;

namespace Datadog.Trace.DuckTyping;

/// <summary>
/// Duck type async awaiter interface
/// </summary>
/// <typeparam name="T">Type of the result</typeparam>
internal interface IDuckTypeAwaiter<out T> : ICriticalNotifyCompletion, IDuckType
{
    /// <summary>
    /// Gets a value indicating whether if the async operation is completed
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// Gets the result of the async operation
    /// </summary>
    /// <returns>Result instance</returns>
    T GetResult();
}

/// <summary>
/// Duck type async awaiter interface
/// </summary>
internal interface IDuckTypeAwaiter : ICriticalNotifyCompletion, IDuckType
{
    /// <summary>
    /// Gets a value indicating whether if the async operation is completed
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// Gets the result of the async operation
    /// </summary>
    void GetResult();
}
