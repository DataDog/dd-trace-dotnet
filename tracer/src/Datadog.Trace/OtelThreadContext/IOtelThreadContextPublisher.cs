// <copyright file="IOtelThreadContextPublisher.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.OtelThreadContext;

/// <summary>
/// Publishes the active trace context of the current thread using the OTEP 4947 thread context protocol,
/// so out-of-process readers can attribute their observations to the same trace and span.
/// </summary>
internal interface IOtelThreadContextPublisher
{
    /// <summary>
    /// Gets a value indicating whether contexts are being published. When <c>false</c>, <see cref="Set"/>
    /// and <see cref="Reset"/> do nothing and the caller can skip calling them altogether.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Publishes <paramref name="span"/> as the context active on the current thread.
    /// </summary>
    void Set(Span span);

    /// <summary>
    /// Publishes "no context active" for the current thread.
    /// </summary>
    void Reset();
}
