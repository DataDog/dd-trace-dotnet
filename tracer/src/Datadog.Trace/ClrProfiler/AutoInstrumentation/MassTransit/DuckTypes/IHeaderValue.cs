// <copyright file="IHeaderValue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;

/// <summary>
/// Minimal duck-typing interface for MassTransit HeaderValue struct
/// Used for extracting key/value pairs from message headers
/// </summary>
internal interface IHeaderValue
{
    /// <summary>
    /// Gets the header key/name
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Gets the header value
    /// </summary>
    object Value { get; }
}
