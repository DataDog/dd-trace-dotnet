// <copyright file="Diagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Microsoft.CodeAnalysis;

namespace Datadog.Trace.Tools.Analyzers.TestingAnalyzer;

/// <summary>
/// Diagnostic definitions for test-related analyzers.
/// </summary>
public class Diagnostics
{
    /// <summary>
    /// The DiagnosticID for <see cref="MissingEnvironmentRestorerRule"/>
    /// </summary>
    public const string MissingEnvironmentRestorerDiagnosticId = "DD0014";

    /// <summary>
    /// The DiagnosticID for <see cref="MissingEnvironmentRestorerNonConstantRule"/>
    /// </summary>
    public const string MissingEnvironmentRestorerNonConstantDiagnosticId = "DD0014";

    /// <summary>
    /// The DiagnosticID for <see cref="RedundantEnvironmentRestorerRule"/>
    /// </summary>
    public const string RedundantEnvironmentRestorerDiagnosticId = "DD0013";

#pragma warning disable RS2008 // Enable analyzer release tracking for the analyzer project

    internal static readonly DiagnosticDescriptor MissingEnvironmentRestorerRule = new(
        MissingEnvironmentRestorerDiagnosticId,
        title: "Environment variable set without [EnvironmentRestorer]",
        messageFormat: "Environment variable '{0}' is set without a corresponding [EnvironmentRestorer(\"{0}\")] attribute — add it at the method or class level to ensure the variable is restored after the test",
        category: "Testing",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "Setting environment variables in tests without [EnvironmentRestorer] can cause test flakiness due to environment state leaking between tests. Use [EnvironmentRestorer(\"VAR_NAME\")] to automatically save and restore the variable.");

    internal static readonly DiagnosticDescriptor MissingEnvironmentRestorerNonConstantRule = new(
        MissingEnvironmentRestorerNonConstantDiagnosticId,
        title: "Environment variable set without [EnvironmentRestorer]",
        messageFormat: "Environment variable is set using a non-constant name — use a constant for the variable name and add [EnvironmentRestorer] at the method or class level, or suppress with #pragma",
        category: "Testing",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "Setting environment variables in tests without [EnvironmentRestorer] can cause test flakiness due to environment state leaking between tests. Use a constant for the variable name so the analyzer can verify [EnvironmentRestorer] coverage.");

    internal static readonly DiagnosticDescriptor RedundantEnvironmentRestorerRule = new(
        RedundantEnvironmentRestorerDiagnosticId,
        title: "Redundant [EnvironmentRestorer] — consider consolidating",
        messageFormat: "{0}",
        category: "Testing",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: false,
        description: "When the same variable appears in [EnvironmentRestorer] on multiple methods, move it to the class level. When a method-level variable is already covered by a class-level [EnvironmentRestorer], remove the redundant method-level attribute.");

#pragma warning restore RS2008
}
