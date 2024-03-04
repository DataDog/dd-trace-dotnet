// <copyright file="ISpanContextProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;

/// <summary>
/// Duck type for custom ISpanContext implementations in Datadog.Trace.Manual
/// </summary>
[DuckCopy]
// ReSharper disable once InconsistentNaming
internal struct ISpanContextProxy
{
    public ulong TraceId;
    public ulong SpanId;
    public string? ServiceName;
}
