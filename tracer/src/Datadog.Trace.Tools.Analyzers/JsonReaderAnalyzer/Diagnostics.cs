// <copyright file="Diagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Microsoft.CodeAnalysis;

namespace Datadog.Trace.Tools.Analyzers.JsonReaderAnalyzer;

/// <summary>
/// Helper class for holding all the diagnostic definitions for the <see cref="JsonArrayPoolAnalyzer"/>.
/// </summary>
public static class Diagnostics
{
    /// <summary>
    /// The DiagnosticID for <see cref="UseJsonArrayPoolRule"/>.
    /// </summary>
    public const string UseJsonArrayPoolDiagnosticId = "DD0016";

    /// <summary>
    /// Reported when a <c>JsonTextReader</c> or <c>JsonTextWriter</c> is constructed without setting
    /// its <c>ArrayPool</c> to <c>JsonArrayPool.Shared</c>. Without it, Newtonsoft.Json allocates a fresh char[]
    /// buffer for every read/write instead of renting from the shared pool, which is particularly costly on
    /// hot paths like RCM, WAF configuration, telemetry, and feature flag parsing. This pattern has been flagged
    /// in code review (e.g. a nested "nit: use the array pool where possible" suggestion) and is mechanically
    /// detectable: every existing call site in Datadog.Trace already follows this convention.
    /// </summary>
    internal static readonly DiagnosticDescriptor UseJsonArrayPoolRule = new(
        UseJsonArrayPoolDiagnosticId,
        title: "Use JsonArrayPool.Shared with JsonTextReader/JsonTextWriter",
        messageFormat: "'{0}' should set 'ArrayPool = JsonArrayPool.Shared' to avoid allocating a new char[] buffer per instance",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The vendored Newtonsoft.Json library allocates a new char[] buffer per JsonTextReader/JsonTextWriter unless an ArrayPool is supplied. Set 'ArrayPool = JsonArrayPool.Shared' (via object initializer or assignment) to re-use the shared buffer pool instead.");
}
