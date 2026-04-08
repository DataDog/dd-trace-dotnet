// <copyright file="Diagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Microsoft.CodeAnalysis;

namespace Datadog.Trace.Tools.Analyzers.AllocationAnalyzer;

/// <summary>
/// Diagnostic definitions for allocation-related analyzers
/// </summary>
public class Diagnostics
{
    /// <summary>
    /// The DiagnosticID for <see cref="NumericToStringInLogRule"/>
    /// </summary>
    public const string NumericToStringInLogDiagnosticId = "DDALLOC001";

    internal static readonly DiagnosticDescriptor NumericToStringInLogRule = new(
        NumericToStringInLogDiagnosticId,
        title: "Unnecessary ToString() on numeric type in log call",
        messageFormat: "Remove unnecessary '{0}.ToString()' call — the generic log overload handles numeric formatting without allocating a string",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Calling .ToString() on numeric types when passing them as log arguments causes an unnecessary string allocation. The generic log overloads handle formatting directly.");
}
