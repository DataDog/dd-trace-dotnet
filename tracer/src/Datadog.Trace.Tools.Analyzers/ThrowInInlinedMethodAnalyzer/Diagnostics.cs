// <copyright file="Diagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Microsoft.CodeAnalysis;

namespace Datadog.Trace.Tools.Analyzers.ThrowInInlinedMethodAnalyzer;

/// <summary>
/// Diagnostic definitions for <see cref="ThrowInInlinedMethodAnalyzer"/>
/// </summary>
public static class Diagnostics
{
    /// <summary>
    /// The diagnostic ID for throw statements in AggressiveInlining methods
    /// </summary>
    public const string DiagnosticId = "DD0011";

#pragma warning disable RS2008 // Enable analyzer release tracking
    internal static readonly DiagnosticDescriptor ThrowInAggressiveInliningRule = new(
        DiagnosticId,
        title: "Avoid throw in AggressiveInlining method",
        messageFormat: "Method '{0}' is marked [AggressiveInlining] but contains a throw statement, which prevents inlining. Extract the throw to a ThrowHelper method with [DoesNotReturn] and [MethodImpl(MethodImplOptions.NoInlining)].",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "throw statements prevent the JIT from inlining a method. Move the throw to a separate helper method marked with [MethodImpl(MethodImplOptions.NoInlining)] and [DoesNotReturn].");

    internal static readonly DiagnosticDescriptor RethrowInAggressiveInliningRule = new(
        DiagnosticId,
        title: "Avoid throw in AggressiveInlining method",
        messageFormat: "Method '{0}' is marked [AggressiveInlining] but contains a rethrow statement, which prevents inlining. Extract the try/catch block to a separate non-inlined method.",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "throw statements (including rethrows) prevent the JIT from inlining a method. Move the try/catch block to a separate method marked with [MethodImpl(MethodImplOptions.NoInlining)].");
#pragma warning restore RS2008
}
