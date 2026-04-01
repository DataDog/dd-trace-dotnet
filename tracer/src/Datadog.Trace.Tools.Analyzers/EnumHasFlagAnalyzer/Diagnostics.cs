// <copyright file="Diagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Microsoft.CodeAnalysis;

namespace Datadog.Trace.Tools.Analyzers.EnumHasFlagAnalyzer;

/// <summary>
/// Diagnostic definitions for the Enum.HasFlag() boxing analyzer
/// </summary>
public class Diagnostics
{
    /// <summary>
    /// The diagnostic ID for <see cref="EnumHasFlagBoxingRule"/>
    /// </summary>
    public const string DiagnosticId = "DDALLOC004";

    /// <summary>
    /// Key used in diagnostic properties to indicate whether HasFlagFast is available
    /// </summary>
    public const string HasFlagFastAvailableKey = "HasFlagFastAvailable";

    internal static readonly DiagnosticDescriptor EnumHasFlagBoxingRule = new(
        DiagnosticId,
        title: "Enum.HasFlag() causes boxing allocations",
        messageFormat: "Enum.HasFlag() boxes both operands. Use HasFlagFast() extension method or bitwise operations instead.",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "Enum.HasFlag() boxes both the receiver and argument on .NET Framework and pre-.NET 7. Replace with HasFlagFast() if available, or use bitwise AND comparison to avoid allocations.");
}
