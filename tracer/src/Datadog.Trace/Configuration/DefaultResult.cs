// <copyright file="DefaultResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration;

internal readonly struct DefaultResult<T>(T result)
{
    /// <summary>
    /// Gets the value to use as the default result
    /// </summary>
    public T Result { get; } = result;
}
