// <copyright file="ConfigKeysNoPreprocessorDirsAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Datadog.Trace.Tools.Analyzers.ConfigurationAnalyzers;

/// <summary>
/// DD0011: Preprocessor directives are forbidden in ConfigurationKeys.
///
/// Forbids any preprocessor directives (e.g., #if/#endif/#define/#undef/#pragma/#region) inside
/// the Datadog.Trace.Configuration.ConfigurationKeys partial class (across all partial files).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConfigKeysNoPreprocessorDirsAnalyzer : DiagnosticAnalyzer
{
    private const string DiagnosticId = "DD0011";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Preprocessor directives are not allowed in ConfigurationKeys",
        messageFormat: "Preprocessor directives '{0}' is not allowed inside Datadog.Trace.Configuration.ConfigurationKeys",
        category: "CodeQuality",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Do not use preprocessor directives inside the ConfigurationKeys partial class. Use runtime configuration or source generators instead.");

    /// <summary>
    /// Gets SupportedDiagnostics
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    /// <summary>
    /// Initialize
    /// </summary>
    /// <param name="context">context</param>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;

        // Quick name check
        if (classDecl.Identifier.ValueText != "ConfigurationKeys")
        {
            return;
        }

        // Ensure it's in the Datadog.Trace.Configuration namespace
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDecl, context.CancellationToken);
        if (symbol is null)
        {
            return;
        }

        var containingNs = symbol.ContainingNamespace?.ToDisplayString();
        if (containingNs != "Datadog.Trace.Configuration")
        {
            return;
        }

        // Look for any preprocessor directives within this class declaration span
        // We consider any DirectiveTriviaSyntax as forbidden
        foreach (var trivia in classDecl.DescendantTrivia(descendIntoTrivia: true))
        {
            if (!trivia.IsDirective)
            {
                continue;
            }

            if (trivia.GetStructure() is DirectiveTriviaSyntax directive)
            {
                // Report on the directive keyword
                var tokenText = directive.ToFullString().Trim();
                var location = directive.GetLocation();
                var diagnostic = Diagnostic.Create(Rule, location, tokenText.Split('\n').FirstOrDefault() ?? tokenText);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
