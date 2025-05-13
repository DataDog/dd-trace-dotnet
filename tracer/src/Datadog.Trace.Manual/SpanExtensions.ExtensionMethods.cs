// <copyright file="SpanExtensions.ExtensionMethods.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// ReSharper disable once CheckNamespace

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.ExtensionMethods;

/// <summary>
/// Extension methods for the <see cref="ISpan"/> interface.
/// </summary>
public static class SpanExtensions
{
    /// <summary>
    /// Sets the sampling priority for the trace that contains the specified <see cref="ISpan"/>.
    /// </summary>
    /// <param name="span">A span that belongs to the trace.</param>
    /// <param name="samplingPriority">The new sampling priority for the trace.</param>
    [Instrumented]
    public static void SetTraceSamplingPriority(this ISpan span, SamplingPriority samplingPriority)
    {
    }
}
