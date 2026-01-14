// <copyright file="PlatformKeysAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Immutable;
using System.Linq;
using Datadog.Trace.Tools.Analyzers.Helpers;
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

    private static readonly string[] ForbiddenPrefixes = ["OTEL", "DD_", "_DD_", "DATADOG_"];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Invalid PlatformKeys constant naming",
        messageFormat: "PlatformKeys constant '{0}' should not start with '{1}'. Platform keys should represent external environment variables, not Datadog or OpenTelemetry configuration keys. Use ConfigurationKeys instead.",
        category: "CodeQuality",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Constants in PlatformKeys class should not start with OTEL, DD_, or _DD_ prefixes as these are reserved for OpenTelemetry and Datadog configuration keys. Platform keys should represent environment variables from external platforms and services.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule, Helpers.Diagnostics.MissingRequiredType];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationContext.Compilation);
            var platformKeysType = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.PlatformKeys);

            if (Helpers.Diagnostics.IsTypeNullAndReportForDatadogTrace(compilationContext, platformKeysType, nameof(PlatformKeysAnalyzer), WellKnownTypeNames.PlatformKeys))
            {
                return;
            }

            compilationContext.RegisterSymbolAction(
                c => AnalyzeNamedType(c, platformKeysType),
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, INamedTypeSymbol platformKeysType)
    {
        var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

        // Only analyze the top-level PlatformKeys class directly
        // Nested classes will be analyzed recursively via AnalyzeConstFields
        if (!SymbolEqualityComparer.Default.Equals(namedTypeSymbol, platformKeysType))
        {
            return;
        }

        // Analyze all const fields in the PlatformKeys class and its nested classes
        AnalyzeConstFields(context, namedTypeSymbol);
    }

    private static void AnalyzeConstFields(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol)
    {
        // Analyze const fields in the current type
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is IFieldSymbol { IsConst: true, Type.SpecialType: SpecialType.System_String } field)
            {
                if (field.ConstantValue is string constantValue)
                {
                    // Check if the constant value starts with any forbidden prefix (case-insensitive)
                    var forbiddenPrefix = ForbiddenPrefixes.FirstOrDefault(prefix => constantValue.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                    if (forbiddenPrefix != null)
                    {
                        var diagnostic = Diagnostic.Create(
                            Rule,
                            field.Locations.FirstOrDefault(),
                            constantValue,
                            forbiddenPrefix);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
            else if (member is INamedTypeSymbol nestedType)
            {
                // Recursively analyze nested classes (like Aws, AzureAppService, etc.)
                AnalyzeConstFields(context, nestedType);
            }
        }
    }
}
