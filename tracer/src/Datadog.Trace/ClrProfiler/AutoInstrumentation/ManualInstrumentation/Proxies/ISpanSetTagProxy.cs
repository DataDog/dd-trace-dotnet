// <copyright file="ISpanSetTagProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;

/// <summary>
/// Duck type used to call SetTag where we receive a custom implementation
/// </summary>
[DuckType("Datadog.Trace.ISpan", "Datadog.Trace.Manual")]
internal interface ISpanSetTagProxy
{
    object SetTag(string key, string? value);
}
