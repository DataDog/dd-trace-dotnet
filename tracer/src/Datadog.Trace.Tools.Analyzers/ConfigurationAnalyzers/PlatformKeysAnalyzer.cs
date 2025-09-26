// <copyright file="PlatformKeysAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Datadog.Trace.Tools.Analyzers.ConfigurationAnalyzers;

/// <summary>
/// DD0010: Invalid PlatformKeys constant naming
///
/// Ensures that constants in the PlatformKeys class do not start with reserved prefixes:
/// - OTEL (OpenTelemetry prefix)
/// - DD_ (Datadog configuration prefix)
/// - _DD_ (Internal Datadog configuration prefix)
/// - DATADOG_ (Older Datadog configuration prefix)
///
/// Platform keys should represent environment variables from external platforms/services,
/// not Datadog-specific or OpenTelemetry configuration keys.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PlatformKeysAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic ID displayed in error messages
    /// </summary>
    public const string DiagnosticId = "DD0010";

    private const string PlatformKeysClassName = "PlatformKeys";
    private const string PlatformKeysNamespace = "Datadog.Trace.Configuration";

    private static readonly string[] ForbiddenPrefixes = { "OTEL", "DD_", "_DD_", "DATADOG_  " };

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Invalid PlatformKeys constant naming",
        messageFormat: "PlatformKeys constant '{0}' should not start with '{1}'. Platform keys should represent external environment variables, not Datadog or OpenTelemetry configuration keys. Use ConfigurationKeys instead.",
        category: "CodeQuality",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Constants in PlatformKeys class should not start with OTEL, DD_, or _DD_ prefixes as these are reserved for OpenTelemetry and Datadog configuration keys. Platform keys should represent environment variables from external platforms and services.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

        // Check if this is the PlatformKeys class in the correct namespace
        if (!IsPlatformKeysClass(namedTypeSymbol))
        {
            return;
        }

        // Analyze all const fields in the PlatformKeys class and its nested classes
        AnalyzeConstFields(context, namedTypeSymbol);
    }

    private static bool IsPlatformKeysClass(INamedTypeSymbol namedTypeSymbol)
    {
        // Check if this is the PlatformKeys class (including partial classes)
        if (namedTypeSymbol.Name != PlatformKeysClassName)
        {
            return false;
        }

        // Check if it's in the correct namespace
        var containingNamespace = namedTypeSymbol.ContainingNamespace;
        return containingNamespace?.ToDisplayString() == PlatformKeysNamespace;
    }

    private static void AnalyzeConstFields(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol)
    {
        // Analyze const fields in the current type
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is IFieldSymbol { IsConst: true, Type.SpecialType: SpecialType.System_String } field)
            {
                AnalyzeConstField(context, field);
            }
            else if (member is INamedTypeSymbol nestedType)
            {
                // Recursively analyze nested classes (like Aws, AzureAppService, etc.)
                AnalyzeConstFields(context, nestedType);
            }
        }
    }

    private static void AnalyzeConstField(SymbolAnalysisContext context, IFieldSymbol field)
    {
        if (field.ConstantValue is not string constantValue)
        {
            return;
        }

        // Check if the constant value starts with any forbidden prefix (case-insensitive)
        var forbiddenPrefix = ForbiddenPrefixes.FirstOrDefault(prefix => constantValue.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (forbiddenPrefix == null)
        {
            return;
        }

        var diagnostic = Diagnostic.Create(
            Rule,
            field.Locations.FirstOrDefault(),
            constantValue,
            forbiddenPrefix);

        context.ReportDiagnostic(diagnostic);
    }
}
