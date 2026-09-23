// <copyright file="Diagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Microsoft.CodeAnalysis;

namespace Datadog.Trace.Tools.Analyzers.StringUtilAnalyzer;

/// <summary>
/// Helper class for holding all the diagnostic definitions for <see cref="StringUtilAnalyzer"/>.
/// </summary>
public class Diagnostics
{
    /// <summary>
    /// The DiagnosticID for <see cref="UseStringUtilRule"/>.
    /// </summary>
    public const string DiagnosticId = "DDSTR001";

    /// <summary>
    /// This repository ships a hand-written <c>StringUtil.IsNullOrEmpty()</c> / <c>StringUtil.IsNullOrWhiteSpace()</c>
    /// wrapper (see <c>tracer/src/Datadog.Trace/Util/StringUtil.cs</c>) purely to add the <c>[NotNullWhen(false)]</c>
    /// nullable annotation that <see cref="string.IsNullOrEmpty(string)"/> and
    /// <see cref="string.IsNullOrWhiteSpace(string)"/> lack on .NET Framework and .NET Standard 2.0. Calling the BCL
    /// method directly instead of the wrapper means the compiler cannot narrow the argument to non-null after the
    /// check on those older target frameworks, which silently reintroduces nullable-reference-type warnings/bugs
    /// that the wrapper exists to prevent. AGENTS.md already documents "Use StringUtil.IsNullOrEmpty() instead of
    /// string.IsNullOrEmpty() for compatibility across all supported runtimes" as a required convention, and this
    /// exact substitution has been called out and fixed by reviewers/agents across multiple recent PRs (e.g. PRs
    /// touching QuartzTests.cs, XUnitEvpTestsV3.cs, XUnitImpactedTests.cs, HashHelperTests.cs). This analyzer
    /// mechanically enforces that convention going forward instead of relying on a human/agent to catch it in review.
    /// </summary>
    internal static readonly DiagnosticDescriptor UseStringUtilRule = new(
        DiagnosticId,
        title: "Use StringUtil instead of string.IsNullOrEmpty/IsNullOrWhiteSpace",
        messageFormat: "Use StringUtil.{0}() instead of string.{0}() for nullable-annotation compatibility across all supported target frameworks",
        category: "CodeQuality",
        defaultSeverity: DiagnosticSeverity.Info, // Info repo-wide by default so this doesn't break existing call sites; escalated to error via .editorconfig in newer/actively-maintained directories.
        isEnabledByDefault: true,
        description: "string.IsNullOrEmpty()/string.IsNullOrWhiteSpace() lack the [NotNullWhen(false)] nullable annotation on .NET Framework and .NET Standard 2.0. Use the repository's StringUtil wrapper instead, which provides the annotation on every supported target framework.");
}
