// <copyright file="ISendHeaders.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit;

/// <summary>
/// Duck-typing interface for MassTransit.SendHeaders (for writing headers on send side)
/// </summary>
internal interface ISendHeaders
{
    /// <summary>
    /// Sets a header value
    /// </summary>
    /// <param name="key">The header key</param>
    /// <param name="value">The header value</param>
    /// <param name="overwrite">Whether to overwrite existing value (default true)</param>
    void Set(string key, object? value, bool overwrite = true);
}
