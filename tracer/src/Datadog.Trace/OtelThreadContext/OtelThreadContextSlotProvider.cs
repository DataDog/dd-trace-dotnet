// <copyright file="OtelThreadContextSlotProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.OtelThreadContext;

/// <summary>
/// Resolves the <c>otel_thread_ctx_v1</c> slot from the native tracer, which defines and exports it
/// as an ELF TLS symbol. See docs/OTelContextPropagation.md.
/// </summary>
internal sealed class OtelThreadContextSlotProvider : IOtelThreadContextSlotProvider
{
    public static readonly OtelThreadContextSlotProvider Instance = new();

    private OtelThreadContextSlotProvider()
    {
    }

    public IntPtr GetSlot() => NativeMethods.GetOtelThreadContextSlot();
}
