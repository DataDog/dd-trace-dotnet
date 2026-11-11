// <copyright file="IProcessLifetimeRootSpan.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace;

/// <summary>
/// Represents an optional root span whose lifetime is bounded by the instrumented process.
/// </summary>
internal interface IProcessLifetimeRootSpan
{
    /// <summary>
    /// Gets the context used as the fallback parent when no distributed or active context exists.
    /// </summary>
    SpanContext? SpanContext { get; }

    /// <summary>
    /// Completes the process root before the tracer's final flush.
    /// </summary>
    /// <param name="exception">The unhandled exception that initiated shutdown, if any.</param>
    void Complete(Exception? exception);
}
