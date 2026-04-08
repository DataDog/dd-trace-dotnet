// <copyright file="Diagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Microsoft.CodeAnalysis;

namespace Datadog.Trace.Tools.Analyzers.StringBuilderCacheAnalyzer;

/// <summary>
/// Diagnostic descriptors for the StringBuilderCache analyzer.
/// </summary>
public sealed class Diagnostics
{
    /// <summary>
    /// The diagnostic ID for <see cref="UseStringBuilderCacheRule"/>.
    /// </summary>
    public const string DiagnosticId = "DDALLOC003";

    internal static readonly DiagnosticDescriptor UseStringBuilderCacheRule = new(
        DiagnosticId,
        title: "Use StringBuilderCache instead of new StringBuilder()",
        messageFormat: "Use StringBuilderCache.Acquire() instead of new StringBuilder() to avoid heap allocation. Call StringBuilderCache.GetStringAndRelease() when done.",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "StringBuilderCache uses a [ThreadStatic] cached instance to avoid allocating a new StringBuilder per call.");
}
