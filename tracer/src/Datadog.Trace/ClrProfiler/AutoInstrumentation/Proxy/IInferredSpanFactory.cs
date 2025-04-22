// <copyright file="IInferredSpanFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;

/// <summary>
/// Supports creating inferred spans from inferred proxy data.
/// </summary>
internal interface IInferredSpanFactory
{
    /// <summary>
    /// Creates and <em>starts</em> a new <see cref="Span"/> representing a proxy request.
    /// </summary>
    /// <param name="tracer">The <see cref="Tracer"/> instance to use to create the <see cref="Span"/>.</param>
    /// <param name="data">The <see cref="InferredProxyData"/> containing the metadata about the proxy request.</param>
    /// <param name="parent">Optional parent context.</param>
    /// <returns>A new <see cref="Scope"/> containing the proxy <see cref="Span"/>; <see langword="null"/> when creation fails.</returns>
    Scope? CreateSpan(
        Tracer tracer,
        InferredProxyData data,
        ISpanContext? parent = null);
}
