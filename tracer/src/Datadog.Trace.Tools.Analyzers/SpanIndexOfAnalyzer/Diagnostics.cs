// <copyright file="Diagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Microsoft.CodeAnalysis;

namespace Datadog.Trace.Tools.Analyzers.SpanIndexOfAnalyzer;

/// <summary>
/// Helper class for holding all the diagnostic definitions.
/// </summary>
public class Diagnostics
{
    /// <summary>
    /// The DiagnosticID for <see cref="UseContainsInsteadOfIndexOfRule"/>.
    /// </summary>
    public const string DiagnosticId = "DD0016";

    /// <summary>
    /// This analyzer exists because of a recurring code review theme: hand-written parsers under
    /// tracer/src/Datadog.Trace (e.g. the SourceLink URL parsers, and the OpenTelemetry thread-context
    /// /proc/self/maps parser) use <c>ReadOnlySpan&lt;char&gt;</c>/<c>Span&lt;char&gt;</c> precisely to avoid
    /// allocating strings, but then write <c>span.IndexOf(x) &gt;= 0</c> (or the equivalent "!= -1"/"&gt; -1"
    /// comparisons) to test for containment. <c>Span&lt;T&gt;.Contains</c> expresses the same intent directly,
    /// is at least as fast (IndexOf has to track and return the position, which the caller then throws away),
    /// and was the exact fix requested - and applied - in review for this pattern. The general string
    /// equivalent of this check (CA2249) already ships as an analyzer rule, but it does not cover the
    /// Span/ReadOnlySpan overloads of IndexOf, which is exactly the API shape these hand-rolled parsers use.
    /// </summary>
    internal static readonly DiagnosticDescriptor UseContainsInsteadOfIndexOfRule = new(
        DiagnosticId,
        title: "Use Span<T>.Contains instead of comparing Span<T>.IndexOf to a sentinel value",
        messageFormat: "Use '{0}.Contains(...)' instead of comparing the result of '{0}.IndexOf(...)' to a sentinel value when the index itself is not used",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When the result of Span<T>/ReadOnlySpan<T>.IndexOf is only compared against a sentinel " +
                     "(>= 0, > -1, < 0, == -1, != -1) to test for containment, prefer the Contains method. It " +
                     "expresses the intent directly and avoids computing and discarding an index.");
}
