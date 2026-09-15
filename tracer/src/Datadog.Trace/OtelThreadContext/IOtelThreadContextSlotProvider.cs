// <copyright file="IOtelThreadContextSlotProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.OtelThreadContext;

/// <summary>
/// Provides the address of the calling thread's <c>otel_thread_ctx_v1</c> thread-local slot.
/// This is the single point of contact with native code on the whole feature, which is why it is
/// abstracted: it lets the publisher be exercised without the native library, on any platform.
/// </summary>
internal interface IOtelThreadContextSlotProvider
{
    /// <summary>
    /// Gets the address of the calling thread's slot, or <see cref="IntPtr.Zero"/> if unavailable.
    /// Called once per OS thread; the result is stable for the lifetime of that thread.
    /// </summary>
    IntPtr GetSlot();
}
