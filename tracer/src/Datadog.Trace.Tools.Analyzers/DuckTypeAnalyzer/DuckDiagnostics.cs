// <copyright file="DuckDiagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Microsoft.CodeAnalysis;

namespace Datadog.Trace.Tools.Analyzers.DuckTypeAnalyzer;

/// <summary>
/// Helper class for holding various diagnostics on ducks.
/// </summary>
public class DuckDiagnostics
{
    /// <summary>
    /// The DiagnosticID for duck type null check rule.
    /// </summary>
    public const string DuckTypeNullCheckDiagnosticId = "DDDUCK001";

    internal static readonly DiagnosticDescriptor ADuckIsNeverNullRule = new(
        DuckTypeNullCheckDiagnosticId,
        title: "Checking IDuckType for null",
        messageFormat: "The IDuckType is almost always non-null, check the Instance for null to ensure we have access to the duck typed object",
        category: "CodeQuality",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
