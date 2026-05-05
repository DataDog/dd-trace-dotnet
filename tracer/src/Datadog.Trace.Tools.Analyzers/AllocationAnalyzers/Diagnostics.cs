// <copyright file="Diagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Microsoft.CodeAnalysis;

namespace Datadog.Trace.Tools.Analyzers.AllocationAnalyzers;

/// <summary>
/// Diagnostic definitions for allocation analyzers.
/// </summary>
public static class Diagnostics
{
    /// <summary>
    /// The DiagnosticID for <see cref="JsonArrayPoolRule"/>
    /// </summary>
    public const string JsonArrayPoolDiagnosticId = "DDALLOC002";

#pragma warning disable RS2008 // Enable analyzer release tracking for the analyzer project
    internal static readonly DiagnosticDescriptor JsonArrayPoolRule = new(
        JsonArrayPoolDiagnosticId,
        title: "Use JsonArrayPool with Newtonsoft.Json readers/writers",
        messageFormat: "Set ArrayPool = JsonArrayPool.Shared on {0} to avoid char[] allocations",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "Without ArrayPool, Newtonsoft.Json allocates internal char[] buffers on every read/write. Use JsonArrayPool.Shared to pool these buffers.");
#pragma warning restore RS2008
}
