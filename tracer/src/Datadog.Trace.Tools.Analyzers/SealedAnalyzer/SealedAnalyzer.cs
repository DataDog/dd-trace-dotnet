// <copyright file="SealedAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Immutable;
using System.Linq;
using Datadog.Trace.Tools.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Datadog.Trace.Tools.Analyzers.SealedAnalyzer;

/// <summary>
/// An analyzer that makes sure you mark types as sealed where possible
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class SealedAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Diagnostics.TypesShouldBeSealedRule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var attributesToMatch = new[]
        {
            context.Compilation.GetOrCreateTypeByMetadataName("System.Runtime.InteropServices.ComImportAttribute"),
        };

        var candidateTypes = PooledConcurrentSet<INamedTypeSymbol>.GetInstance(SymbolEqualityComparer.Default);
        var baseTypes = PooledConcurrentSet<INamedTypeSymbol>.GetInstance(SymbolEqualityComparer.Default);

        context.RegisterSymbolAction(
            context =>
            {
                var type = (INamedTypeSymbol)context.Symbol;

                if (type.TypeKind is TypeKind.Class &&
                    !type.IsAbstract &&
                    !type.IsStatic &&
                    !type.IsSealed &&
                    !type.HasAnyAttribute(attributesToMatch) &&
                    !type.IsTopLevelStatementsEntryPointType())
                {
                    candidateTypes.Add(type);
                }

                for (INamedTypeSymbol? baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
                {
                    baseTypes.Add(baseType.OriginalDefinition);
                }
            },
            SymbolKind.NamedType);

        context.RegisterCompilationEndAction(context =>
        {
            foreach (INamedTypeSymbol type in candidateTypes)
            {
                if (!baseTypes.Contains(type.OriginalDefinition))
                {
                    var inSource = type.Locations.Where(l => l.IsInSource);
                    if (!inSource.Any())
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                Diagnostics.TypesShouldBeSealedRule,
                                location: null,
                                type.Name));
                    }
                    else
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                Diagnostics.TypesShouldBeSealedRule,
                                location: inSource.First(),
                                additionalLocations: inSource.Skip(1),
                                messageArgs: type.Name));
                    }
                }
            }

            candidateTypes.Dispose();
            baseTypes.Dispose();
        });
    }
}
