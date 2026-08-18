// <copyright file="Diagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Microsoft.CodeAnalysis;

namespace Datadog.Trace.Tools.Analyzers.CapturingLambdaAnalyzer;

/// <summary>
/// Diagnostic descriptors for the capturing lambda analyzer
/// </summary>
public static class Diagnostics
{
    /// <summary>
    /// The diagnostic ID for <see cref="CapturingLambdaRule"/>
    /// </summary>
    public const string DiagnosticId = "DDALLOC006";

    internal static readonly DiagnosticDescriptor CapturingLambdaRule = new(
        DiagnosticId,
        title: "Avoid capturing lambdas in Task.Run/ContinueWith",
        messageFormat: "Lambda passed to '{0}' captures variable(s) '{1}', causing a closure allocation. Use a static lambda with the state parameter overload, or mark the lambda 'static' if no captures are needed.",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "Capturing lambdas allocate a compiler-generated display class and a delegate object. Use the state parameter overloads (e.g., Task.Factory.StartNew(static state => ..., stateObj)) to avoid closure allocations.");
}
