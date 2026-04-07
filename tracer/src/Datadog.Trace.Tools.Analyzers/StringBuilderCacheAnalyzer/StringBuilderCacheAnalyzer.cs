// <copyright file="StringBuilderCacheAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Immutable;
using System.Linq;
using System.Threading;

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

        // Suppress if the capacity exceeds StringBuilderCache.MaxBuilderSize (360).
        // StringBuilderCache won't cache builders larger than this, so using it
        // would add overhead without any caching benefit.
        if (HasCapacityExceedingMaxBuilderSize(constructorSymbol, context))
        {
            return;
        }

        var enclosingFunction = GetEnclosingFunction(context.Node);

        // Suppress if the enclosing function-like scope already calls StringBuilderCache.Acquire()
        if (enclosingFunction is not null && ContainsStringBuilderCacheAcquireCall(enclosingFunction))
        {
            return;
        }

        // Suppress if there are multiple StringBuilder allocations in the same scope
        // (StringBuilderCache only caches one instance per thread)
        if (enclosingFunction is not null && CountStringBuilderCreations(enclosingFunction, context.SemanticModel, stringBuilderType, context.CancellationToken) > 1)
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
        => IsAssignedToFieldOrProperty(context.Node, context.SemanticModel, context.CancellationToken);

    private static int CountStringBuilderCreations(
        SyntaxNode functionNode,
        SemanticModel semanticModel,
        INamedTypeSymbol stringBuilderType,
        CancellationToken cancellationToken)
    {
        var count = 0;

        // Use descendIntoChildren to skip nested function scopes — they are analyzed independently
        foreach (var node in functionNode.DescendantNodes(descendIntoChildren: n => n is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax) || n == functionNode))
        {
            ITypeSymbol? createdType = node switch
            {
                ObjectCreationExpressionSyntax creation => semanticModel.GetTypeInfo(creation, cancellationToken).Type,
                ImplicitObjectCreationExpressionSyntax creation => semanticModel.GetTypeInfo(creation, cancellationToken).Type,
                _ => null,
            };

            if (createdType is not null
                && SymbolEqualityComparer.Default.Equals(createdType, stringBuilderType)
                && !IsAssignedToFieldOrProperty(node, semanticModel, cancellationToken))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsAssignedToFieldOrProperty(
        SyntaxNode node,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        // Case 1: Field/property initializer
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is FieldDeclarationSyntax or PropertyDeclarationSyntax)
            {
                return true;
            }

            if (current is StatementSyntax or MemberDeclarationSyntax)
            {
                break;
            }
        }

        // Case 2: Constructor assignment to field — e.g., _sb = new StringBuilder(...);
        if (node.Parent is AssignmentExpressionSyntax assignment
            && assignment.Right == node)
        {
            var symbol = semanticModel.GetSymbolInfo(assignment.Left, cancellationToken).Symbol;
            if (symbol is IFieldSymbol or IPropertySymbol)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasCapacityExceedingMaxBuilderSize(IMethodSymbol constructorSymbol, SyntaxNodeAnalysisContext context)
    {
        const int maxBuilderSize = 360; // StringBuilderCache.MaxBuilderSize

        // Find the capacity parameter index
        int capacityIndex = -1;
        for (var i = 0; i < constructorSymbol.Parameters.Length; i++)
        {
            if (constructorSymbol.Parameters[i].Type.SpecialType == SpecialType.System_Int32)
            {
                capacityIndex = i;
                // For StringBuilder(string, int) the int is capacity
                // For StringBuilder(int) the int is capacity
                // For StringBuilder(int, int) the first int is capacity
                // For StringBuilder(string, int, int, int) the last int is capacity
                // In all overloads, we check the first int parameter found — except the 4-arg overload
                if (constructorSymbol.Parameters.Length == 4)
                {
                    // StringBuilder(string, int startIndex, int length, int capacity) — capacity is last
                    capacityIndex = 3;
                }

                break;
            }
        }

        if (capacityIndex < 0)
        {
            return false;
        }

        ArgumentListSyntax? argList = context.Node switch
        {
            ObjectCreationExpressionSyntax oc => oc.ArgumentList,
            ImplicitObjectCreationExpressionSyntax ic => ic.ArgumentList,
            _ => null,
        };

        if (argList is null || capacityIndex >= argList.Arguments.Count)
        {
            return false;
        }

        var capacityArg = argList.Arguments[capacityIndex].Expression;
        var constantValue = context.SemanticModel.GetConstantValue(capacityArg, context.CancellationToken);

        return constantValue is { HasValue: true, Value: int capacity } && capacity > maxBuilderSize;
    }

    private static bool ContainsStringBuilderCacheAcquireCall(SyntaxNode functionNode)
    {
        // Use descendIntoChildren to skip nested function scopes — they are analyzed independently
        foreach (var invocation in functionNode.DescendantNodes(descendIntoChildren: n => n is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax) || n == functionNode).OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Name.Identifier.Text == "Acquire"
                && ExpressionEndsWithStringBuilderCache(memberAccess.Expression))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ExpressionEndsWithStringBuilderCache(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax { Identifier.Text: "StringBuilderCache" } => true,
            MemberAccessExpressionSyntax { Name.Identifier.Text: "StringBuilderCache" } => true,
            _ => false,
        };
    }
}
