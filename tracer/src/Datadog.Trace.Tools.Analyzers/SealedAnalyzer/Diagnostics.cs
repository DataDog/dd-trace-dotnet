// <copyright file="Diagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Microsoft.CodeAnalysis;

namespace Datadog.Trace.Tools.Analyzers.SealedAnalyzer;

/// <summary>
/// Helper class for holding all the diagnostic definitions
/// </summary>
public class Diagnostics
{
    /// <summary>
    /// The DiagnosticID for <see cref="TypesShouldBeSealedRule"/>
    /// </summary>
    public const string DiagnosticId = "DDSEAL001";

    internal static readonly DiagnosticDescriptor TypesShouldBeSealedRule = new(
        DiagnosticId,
        title: "Seal types",
        messageFormat: "Types should be sealed where possible. Types used for duck-typing cannot be sealed. Sealing types can improve performance.",
        "Performance",
        defaultSeverity: DiagnosticSeverity.Info, // Unfortunately, this means it will never appear by default, and must be opted in-to
        isEnabledByDefault: true,
        description: "Seal types to improve performance.",
        customTags: "CompilationEnd");
}
