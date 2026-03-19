// <copyright file="ISendHeaders.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;

/// <summary>
/// Duck-typing interface for MassTransit.SendHeaders.
/// Set(string, string) is a public method on the SendHeaders interface,
/// so duck typing can bind to it even though the concrete implementation
/// (DictionarySendHeaders) uses explicit interface implementation.
/// </summary>
internal interface ISendHeaders
{
    /// <summary>
    /// Sets a header value.
    /// </summary>
    void Set(string key, string value);
}
