// <copyright file="ThrowInInlinedMethodAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Datadog.Trace.Tools.Analyzers.ThrowInInlinedMethodAnalyzer;

/// <summary>
/// Detects throw statements inside methods marked with [MethodImpl(MethodImplOptions.AggressiveInlining)].
/// throw prevents the JIT from inlining the method, defeating the attribute's purpose.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ThrowInInlinedMethodAnalyzer : DiagnosticAnalyzer
{
    private const int AggressiveInliningValue = (int)MethodImplOptions.AggressiveInlining; // 256

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Diagnostics.ThrowInAggressiveInliningRule, Diagnostics.RethrowInAggressiveInliningRule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeThrow, SyntaxKind.ThrowStatement, SyntaxKind.ThrowExpression);
    }

    private static void AnalyzeThrow(SyntaxNodeAnalysisContext context)
    {
        var throwNode = context.Node;

        // Walk up to find the containing method, property accessor, or constructor
        var containingMember = GetContainingMember(throwNode);
        if (containingMember is null)
        {
            return;
        }

        // Check if the member has [MethodImpl(MethodImplOptions.AggressiveInlining)]
        var declaredSymbol = context.SemanticModel.GetDeclaredSymbol(containingMember, context.CancellationToken);
        if (declaredSymbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (HasAggressiveInlining(methodSymbol))
        {
            var memberName = GetMemberDisplayName(containingMember);
            var rule = IsRethrow(throwNode)
                ? Diagnostics.RethrowInAggressiveInliningRule
                : Diagnostics.ThrowInAggressiveInliningRule;
            var diagnostic = Diagnostic.Create(rule, throwNode.GetLocation(), memberName);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsRethrow(SyntaxNode throwNode)
        => throwNode is ThrowStatementSyntax { Expression: null };

    private static SyntaxNode? GetContainingMember(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case MethodDeclarationSyntax:
                case ConstructorDeclarationSyntax:
                case DestructorDeclarationSyntax:
                case OperatorDeclarationSyntax:
                case ConversionOperatorDeclarationSyntax:
                case AccessorDeclarationSyntax:
                case LocalFunctionStatementSyntax:
                    return current;
                // Stop at type boundary — lambdas/anonymous methods create their own scope
                case AnonymousFunctionExpressionSyntax:
                case TypeDeclarationSyntax:
                    return null;
            }
        }

        return null;
    }

    private static bool HasAggressiveInlining(IMethodSymbol methodSymbol)
    {
        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != "System.Runtime.CompilerServices.MethodImplAttribute")
            {
                continue;
            }

            // Check constructor argument: [MethodImpl(MethodImplOptions.AggressiveInlining)]
            if (attribute.ConstructorArguments.Length > 0)
            {
                var arg = attribute.ConstructorArguments[0];
                if (arg.Value is int intValue && (intValue & AggressiveInliningValue) != 0)
                {
                    return true;
                }

                // The constructor also accepts MethodImplOptions (short) cast scenarios
                if (arg.Value is short shortValue && (shortValue & AggressiveInliningValue) != 0)
                {
                    return true;
                }
            }

            // Check named argument: [MethodImpl(Value = MethodImplOptions.AggressiveInlining)]
            foreach (var namedArg in attribute.NamedArguments)
            {
                if (namedArg.Key == "Value" &&
                    namedArg.Value.Value is int namedIntValue &&
                    (namedIntValue & AggressiveInliningValue) != 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string GetMemberDisplayName(SyntaxNode member)
    {
        return member switch
        {
            MethodDeclarationSyntax m => m.Identifier.Text,
            ConstructorDeclarationSyntax c => c.Identifier.Text,
            DestructorDeclarationSyntax d => "~" + d.Identifier.Text,
            OperatorDeclarationSyntax o => "operator " + o.OperatorToken.Text,
            ConversionOperatorDeclarationSyntax co => co.ImplicitOrExplicitKeyword.Text + " operator " + co.Type,
            AccessorDeclarationSyntax a => a.Keyword.Text,
            LocalFunctionStatementSyntax l => l.Identifier.Text,
            _ => "unknown",
        };
    }
}
