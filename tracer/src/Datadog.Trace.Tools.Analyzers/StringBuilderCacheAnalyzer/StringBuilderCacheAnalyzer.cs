// <copyright file="StringBuilderCacheAnalyzer.cs" company="Datadog">
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

namespace Datadog.Trace.Tools.Analyzers.StringBuilderCacheAnalyzer;

/// <summary>
/// An analyzer that detects <c>new StringBuilder()</c> allocations and suggests
/// using <c>StringBuilderCache.Acquire()</c> instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class StringBuilderCacheAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Diagnostics.UseStringBuilderCacheRule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var stringBuilderType = context.Compilation.GetTypeByMetadataName("System.Text.StringBuilder");
        if (stringBuilderType is null)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            ctx => AnalyzeObjectCreation(ctx, stringBuilderType),
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context, INamedTypeSymbol stringBuilderType)
    {
        var symbolInfo = context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol constructorSymbol)
        {
            return;
        }

        // Check that this is a StringBuilder constructor
        if (!SymbolEqualityComparer.Default.Equals(constructorSymbol.ContainingType, stringBuilderType))
        {
            return;
        }

        // Suppress if inside the StringBuilderCache class itself
        var containingType = GetContainingTypeSymbol(context);
        if (containingType is not null && containingType.Name == "StringBuilderCache")
        {
            return;
        }

        // Suppress if assigned to a field or property (long-lived, not method-scoped)
        if (IsAssignedToFieldOrProperty(context))
        {
            return;
        }

        // Suppress if the enclosing function-like scope already calls StringBuilderCache.Acquire()
        var enclosingFunction = GetEnclosingFunction(context.Node);
        if (enclosingFunction is not null && ContainsStringBuilderCacheAcquireCall(enclosingFunction))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Diagnostics.UseStringBuilderCacheRule,
                context.Node.GetLocation()));
    }

    private static INamedTypeSymbol? GetContainingTypeSymbol(SyntaxNodeAnalysisContext context)
    {
        var typeDeclaration = context.Node.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (typeDeclaration is null)
        {
            return null;
        }

        return context.SemanticModel.GetDeclaredSymbol(typeDeclaration, context.CancellationToken) as INamedTypeSymbol;
    }

    private static SyntaxNode? GetEnclosingFunction(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case MethodDeclarationSyntax:
                case LocalFunctionStatementSyntax:
                case AnonymousFunctionExpressionSyntax:
                case AccessorDeclarationSyntax:
                    return current;
            }
        }

        return null;
    }

    private static bool IsAssignedToFieldOrProperty(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node;

        // Case 1: Field initializer — e.g., StringBuilder _sb = new(...);
        // Walk up: ObjectCreation -> EqualsValueClause -> VariableDeclarator -> VariableDeclaration -> FieldDeclaration/PropertyDeclaration
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is FieldDeclarationSyntax or PropertyDeclarationSyntax)
            {
                return true;
            }

            // Stop walking if we hit a statement or member boundary
            if (current is StatementSyntax or MemberDeclarationSyntax)
            {
                break;
            }
        }

        // Case 2: Constructor assignment to field — e.g., _sb = new StringBuilder(...);
        if (node.Parent is AssignmentExpressionSyntax assignment
            && assignment.Right == node)
        {
            var symbol = context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol;
            if (symbol is IFieldSymbol or IPropertySymbol)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsStringBuilderCacheAcquireCall(SyntaxNode functionNode)
    {
        foreach (var invocation in functionNode.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Name.Identifier.Text == "Acquire"
                && memberAccess.Expression is IdentifierNameSyntax identifier
                && identifier.Identifier.Text == "StringBuilderCache")
            {
                return true;
            }
        }

        return false;
    }
}
